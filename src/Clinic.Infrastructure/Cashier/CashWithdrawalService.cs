namespace Clinic.Infrastructure.Cashier;

public sealed class CashWithdrawalService(ClinicDbContext dbContext,
    TimeProvider timeProvider) : ICashWithdrawalService
{
    public async Task<Result<CreatedCashWithdrawalModel>> CreateAsync(
        long actorUserId, Guid idempotencyKey, decimal amount, string reason,
        CancellationToken cancellationToken)
    {
        CashWithdrawal? existing = await Details().SingleOrDefaultAsync(item =>
            item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayCreateAsync(existing, actorUserId, amount, reason,
                cancellationToken);
        }

        DateTimeOffset requestTime = timeProvider.GetUtcNow();
        ShiftTarget? target = await FindCurrentShiftAsync(actorUserId, requestTime,
            cancellationToken);
        if (target is null)
        {
            return Result.Failure<CreatedCashWithdrawalModel>(
                CashierErrors.ShiftNotFound);
        }

        string fingerprint = Fingerprint(actorUserId, target.ShiftId, amount, reason);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireSecretaryAsync(dbContext,
                    actorUserId, cancellationToken);
                await TransactionalResourceLock.AcquireShiftAsync(dbContext,
                    target.ShiftId, cancellationToken);

                CashWithdrawal? replay = await Details(tracking: true)
                    .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey,
                        cancellationToken);
                if (replay is not null)
                {
                    return await ReplayCreateAsync(replay, actorUserId, amount,
                        reason, cancellationToken);
                }

                Shift? shift = await dbContext.Shifts.Include(item => item.CashDrawer)
                    .SingleOrDefaultAsync(item => item.Id == target.ShiftId,
                        cancellationToken);
                DateTimeOffset now = timeProvider.GetUtcNow();
                if (shift is null)
                {
                    return Result.Failure<CreatedCashWithdrawalModel>(
                        CashierErrors.ShiftNotFound);
                }

                if (shift.CashDrawer.SecretaryUserId != actorUserId)
                {
                    return Result.Failure<CreatedCashWithdrawalModel>(
                        CashierErrors.Forbidden);
                }

                if (!shift.CanCollect(now))
                {
                    return Result.Failure<CreatedCashWithdrawalModel>(
                        CashierErrors.Conflict(
                            "لا يمكن طلب سحب نقدي في هذا الشيفت الآن."));
                }

                string withdrawalNumber = await NextWithdrawalNumberAsync(now,
                    cancellationToken);
                CashWithdrawal withdrawal = CashWithdrawal.Create(withdrawalNumber,
                    idempotencyKey, fingerprint, shift.Id, amount, reason,
                    actorUserId, now);
                dbContext.CashWithdrawals.Add(withdrawal);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
                    actorUserId, "cashier.cash_withdrawal_requested",
                    nameof(CashWithdrawal), withdrawal.Id, now, withdrawal.Reason,
                    new { withdrawal.ShiftId, withdrawal.Amount }));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(new CreatedCashWithdrawalModel(
                    await MapAsync(withdrawal, cancellationToken), false));
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<CreatedCashWithdrawalModel>(
                CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<CreatedCashWithdrawalModel>(
                CashierErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (
            CashierInfrastructureSupport.IsUniqueViolation(exception,
                "UX_CashWithdrawals_IdempotencyKey"))
        {
            dbContext.ChangeTracker.Clear();
            CashWithdrawal? replay = await Details().SingleOrDefaultAsync(item =>
                item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return await ReplayCreateAsync(replay, actorUserId, amount, reason,
                cancellationToken);
        }
    }

    public Task<Result<CashWithdrawalModel>> ApproveAsync(long actorUserId,
        long withdrawalId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken) => ReviewAsync(actorUserId,
            withdrawalId, reason, rowVersion, approve: true, cancellationToken);

    public Task<Result<CashWithdrawalModel>> RejectAsync(long actorUserId,
        long withdrawalId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken) => ReviewAsync(actorUserId,
            withdrawalId, reason, rowVersion, approve: false, cancellationToken);

    public async Task<Result<CashWithdrawalModel>> CancelAsync(long actorUserId,
        long withdrawalId, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        WithdrawalTarget? target = await FindTargetAsync(withdrawalId,
            cancellationToken);
        if (target is null)
        {
            return Result.Failure<CashWithdrawalModel>(
                CashierErrors.CashWithdrawalNotFound);
        }

        if (target.SecretaryUserId != actorUserId)
        {
            return Result.Failure<CashWithdrawalModel>(CashierErrors.Forbidden);
        }

        return await MutateAsync(target, async (withdrawal, shift, now) =>
        {
            if (!CashierInfrastructureSupport.MatchesVersion(
                withdrawal.RowVersion, rowVersion))
            {
                return Result.Failure<CashWithdrawalModel>(
                    CashierErrors.ConcurrencyConflict);
            }

            if (shift.Status is ShiftStatus.Closed or ShiftStatus.Cancelled)
            {
                return Result.Failure<CashWithdrawalModel>(CashierErrors.Conflict(
                    "لا يمكن إلغاء طلب يخص شيفت مغلق أو ملغي."));
            }

            withdrawal.Cancel(actorUserId, now);
            dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
                actorUserId, "cashier.cash_withdrawal_cancelled",
                nameof(CashWithdrawal), withdrawal.Id, now,
                data: new { withdrawal.ShiftId, withdrawal.Amount }));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(await MapAsync(withdrawal, cancellationToken));
        }, cancellationToken);
    }

    public async Task<Result<ExecutedCashWithdrawalModel>> ExecuteAsync(
        long actorUserId, long withdrawalId, Guid idempotencyKey,
        byte[] rowVersion, CancellationToken cancellationToken)
    {
        CashWithdrawal? existing = await Details().SingleOrDefaultAsync(item =>
            item.ExecutionIdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayExecutionAsync(existing, actorUserId,
                withdrawalId, cancellationToken);
        }

        WithdrawalTarget? target = await FindTargetAsync(withdrawalId,
            cancellationToken);
        if (target is null)
        {
            return Result.Failure<ExecutedCashWithdrawalModel>(
                CashierErrors.CashWithdrawalNotFound);
        }

        if (target.SecretaryUserId != actorUserId)
        {
            return Result.Failure<ExecutedCashWithdrawalModel>(
                CashierErrors.Forbidden);
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await AcquireLocksAsync(target, cancellationToken);

                CashWithdrawal? replay = await Details(tracking: true)
                    .SingleOrDefaultAsync(item =>
                        item.ExecutionIdempotencyKey == idempotencyKey,
                        cancellationToken);
                if (replay is not null)
                {
                    return await ReplayExecutionAsync(replay, actorUserId,
                        withdrawalId, cancellationToken);
                }

                CashWithdrawal? withdrawal = await Details(tracking: true)
                    .SingleOrDefaultAsync(item => item.Id == withdrawalId,
                        cancellationToken);
                if (withdrawal is null)
                {
                    return Result.Failure<ExecutedCashWithdrawalModel>(
                        CashierErrors.CashWithdrawalNotFound);
                }

                if (withdrawal.Shift.CashDrawer.SecretaryUserId != actorUserId)
                {
                    return Result.Failure<ExecutedCashWithdrawalModel>(
                        CashierErrors.Forbidden);
                }

                if (!CashierInfrastructureSupport.MatchesVersion(
                    withdrawal.RowVersion, rowVersion))
                {
                    return Result.Failure<ExecutedCashWithdrawalModel>(
                        CashierErrors.ConcurrencyConflict);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                if (!withdrawal.Shift.CanCollect(now))
                {
                    return Result.Failure<ExecutedCashWithdrawalModel>(
                        CashierErrors.Conflict(
                            "لا يمكن تنفيذ السحب في هذا الشيفت الآن."));
                }

                decimal currentExpectedCash = await CashierInfrastructureSupport
                    .ExpectedCashAsync(dbContext, withdrawal.ShiftId,
                        withdrawal.Shift.OpeningBalance, cancellationToken);
                if (withdrawal.Amount > currentExpectedCash)
                {
                    return Result.Failure<ExecutedCashWithdrawalModel>(
                        CashierErrors.Conflict(
                            "مبلغ السحب يتجاوز الكاش المتوقع في الدرج."));
                }

                withdrawal.Execute(actorUserId, idempotencyKey, now);
                withdrawal.Shift.RegisterCashWithdrawal(now);
                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
                    actorUserId, "cashier.cash_withdrawal_executed",
                    nameof(CashWithdrawal), withdrawal.Id, now,
                    withdrawal.DecisionReason,
                    new { withdrawal.ShiftId, withdrawal.Amount }));
                await dbContext.SaveChangesAsync(cancellationToken);
                decimal expectedAfter = await CashierInfrastructureSupport
                    .ExpectedCashAsync(dbContext, withdrawal.ShiftId,
                        withdrawal.Shift.OpeningBalance, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(new ExecutedCashWithdrawalModel(
                    await MapAsync(withdrawal, cancellationToken), false,
                    expectedAfter));
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<ExecutedCashWithdrawalModel>(
                CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ExecutedCashWithdrawalModel>(
                CashierErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (
            CashierInfrastructureSupport.IsUniqueViolation(exception,
                "UX_CashWithdrawals_ExecutionIdempotencyKey"))
        {
            dbContext.ChangeTracker.Clear();
            CashWithdrawal? replay = await Details().SingleOrDefaultAsync(item =>
                item.ExecutionIdempotencyKey == idempotencyKey,
                cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return await ReplayExecutionAsync(replay, actorUserId,
                withdrawalId, cancellationToken);
        }
    }

    public async Task<Result<CashWithdrawalModel>> GetAsync(long actorUserId,
        long withdrawalId, bool adminOverride,
        CancellationToken cancellationToken)
    {
        CashWithdrawal? withdrawal = await ApplyAccess(Details(), actorUserId,
            adminOverride).SingleOrDefaultAsync(item => item.Id == withdrawalId,
                cancellationToken);
        return withdrawal is null
            ? Result.Failure<CashWithdrawalModel>(
                CashierErrors.CashWithdrawalNotFound)
            : Result.Success(await MapAsync(withdrawal, cancellationToken));
    }

    public async Task<Result<CashWithdrawalPage>> ListForShiftAsync(
        long actorUserId, long shiftId, bool adminOverride, int pageNumber,
        int pageSize, CancellationToken cancellationToken)
    {
        if (!await CanAccessShiftAsync(actorUserId, shiftId, adminOverride,
            cancellationToken))
        {
            return Result.Failure<CashWithdrawalPage>(CashierErrors.Forbidden);
        }

        IQueryable<CashWithdrawal> query = Details().Where(item =>
            item.ShiftId == shiftId);
        return Result.Success(await PageAsync(query, pageNumber, pageSize,
            cancellationToken));
    }

    public async Task<Result<CashWithdrawalPage>> SearchAsync(
        CashWithdrawalSearch search, CancellationToken cancellationToken)
    {
        IQueryable<CashWithdrawal> query = Details();
        if (search.ShiftId.HasValue)
        {
            query = query.Where(item => item.ShiftId == search.ShiftId.Value);
        }

        if (search.SecretaryUserId.HasValue)
        {
            query = query.Where(item => item.RequestedByUserId ==
                search.SecretaryUserId.Value);
        }

        if (search.Status.HasValue)
        {
            query = query.Where(item => item.Status == search.Status.Value);
        }

        if (search.From.HasValue)
        {
            query = query.Where(item => item.RequestedAt >= search.From.Value);
        }

        if (search.To.HasValue)
        {
            query = query.Where(item => item.RequestedAt <= search.To.Value);
        }

        return Result.Success(await PageAsync(query, search.PageNumber,
            search.PageSize, cancellationToken));
    }

    private async Task<Result<CashWithdrawalModel>> ReviewAsync(long actorUserId,
        long withdrawalId, string reason, byte[] rowVersion, bool approve,
        CancellationToken cancellationToken)
    {
        WithdrawalTarget? target = await FindTargetAsync(withdrawalId,
            cancellationToken);
        if (target is null)
        {
            return Result.Failure<CashWithdrawalModel>(
                CashierErrors.CashWithdrawalNotFound);
        }

        return await MutateAsync(target, async (withdrawal, shift, now) =>
        {
            if (!CashierInfrastructureSupport.MatchesVersion(
                withdrawal.RowVersion, rowVersion))
            {
                return Result.Failure<CashWithdrawalModel>(
                    CashierErrors.ConcurrencyConflict);
            }

            if (shift.Status is ShiftStatus.Closed or ShiftStatus.Cancelled ||
                (approve && !shift.CanCollect(now)))
            {
                return Result.Failure<CashWithdrawalModel>(CashierErrors.Conflict(
                    "لا يمكن مراجعة طلب السحب في حالة الشيفت الحالية."));
            }

            if (approve)
            {
                withdrawal.Approve(actorUserId, now, reason);
            }
            else
            {
                withdrawal.Reject(actorUserId, now, reason);
            }

            dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
                actorUserId, approve ? "cashier.cash_withdrawal_approved" :
                    "cashier.cash_withdrawal_rejected", nameof(CashWithdrawal),
                withdrawal.Id, now, reason,
                new { withdrawal.ShiftId, withdrawal.Amount }));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(await MapAsync(withdrawal, cancellationToken));
        }, cancellationToken);
    }

    private async Task<Result<CashWithdrawalModel>> MutateAsync(
        WithdrawalTarget target,
        Func<CashWithdrawal, Shift, DateTimeOffset,
            Task<Result<CashWithdrawalModel>>> mutation,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await AcquireLocksAsync(target, cancellationToken);
                CashWithdrawal? withdrawal = await Details(tracking: true)
                    .SingleOrDefaultAsync(item => item.Id == target.WithdrawalId,
                        cancellationToken);
                if (withdrawal is null)
                {
                    return Result.Failure<CashWithdrawalModel>(
                        CashierErrors.CashWithdrawalNotFound);
                }

                Result<CashWithdrawalModel> result = await mutation(withdrawal,
                    withdrawal.Shift, timeProvider.GetUtcNow());
                if (result.IsFailure)
                {
                    return result;
                }

                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<CashWithdrawalModel>(
                CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<CashWithdrawalModel>(
                CashierErrors.ConcurrencyConflict);
        }
    }

    private async Task<CashWithdrawalPage> PageAsync(
        IQueryable<CashWithdrawal> query, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        int totalCount = await query.CountAsync(cancellationToken);
        CashWithdrawal[] withdrawals = await query
            .OrderByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return new CashWithdrawalPage(await MapManyAsync(withdrawals,
            cancellationToken), pageNumber, pageSize, totalCount);
    }

    private async Task<Result<CreatedCashWithdrawalModel>> ReplayCreateAsync(
        CashWithdrawal withdrawal, long actorUserId, decimal amount,
        string reason, CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint(actorUserId, withdrawal.ShiftId,
            amount, reason);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(withdrawal.RequestFingerprint),
            Encoding.ASCII.GetBytes(fingerprint)))
        {
            return Result.Failure<CreatedCashWithdrawalModel>(
                CashierErrors.IdempotencyConflict);
        }

        return Result.Success(new CreatedCashWithdrawalModel(
            await MapAsync(withdrawal, cancellationToken), true));
    }

    private async Task<Result<ExecutedCashWithdrawalModel>> ReplayExecutionAsync(
        CashWithdrawal withdrawal, long actorUserId, long withdrawalId,
        CancellationToken cancellationToken)
    {
        if (withdrawal.Id != withdrawalId ||
            withdrawal.ExecutedByUserId != actorUserId ||
            withdrawal.Status != CashWithdrawalStatus.Executed)
        {
            return Result.Failure<ExecutedCashWithdrawalModel>(
                CashierErrors.IdempotencyConflict);
        }

        decimal expectedCash = await CashierInfrastructureSupport.ExpectedCashAsync(
            dbContext, withdrawal.ShiftId, withdrawal.Shift.OpeningBalance,
            cancellationToken);
        return Result.Success(new ExecutedCashWithdrawalModel(
            await MapAsync(withdrawal, cancellationToken), true, expectedCash));
    }

    private async Task AcquireLocksAsync(WithdrawalTarget target,
        CancellationToken cancellationToken)
    {
        await TransactionalResourceLock.AcquireSecretaryAsync(dbContext,
            target.SecretaryUserId, cancellationToken);
        await TransactionalResourceLock.AcquireShiftAsync(dbContext,
            target.ShiftId, cancellationToken);
        await TransactionalResourceLock.AcquireCashWithdrawalAsync(dbContext,
            target.WithdrawalId, cancellationToken);
    }

    private Task<ShiftTarget?> FindCurrentShiftAsync(long actorUserId,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.Shifts.AsNoTracking().Where(item =>
            item.CashDrawer.SecretaryUserId == actorUserId &&
            item.Status != ShiftStatus.Closed &&
            item.Status != ShiftStatus.Cancelled &&
            item.ScheduledStart <= now && now < item.GraceEndsAt)
        .Select(item => new ShiftTarget(item.Id,
            item.CashDrawer.SecretaryUserId))
        .SingleOrDefaultAsync(cancellationToken);

    private Task<WithdrawalTarget?> FindTargetAsync(long withdrawalId,
        CancellationToken cancellationToken) => dbContext.CashWithdrawals
        .AsNoTracking().Where(item => item.Id == withdrawalId)
        .Select(item => new WithdrawalTarget(item.Id, item.ShiftId,
            item.Shift.CashDrawer.SecretaryUserId))
        .SingleOrDefaultAsync(cancellationToken);

    private Task<bool> CanAccessShiftAsync(long actorUserId, long shiftId,
        bool adminOverride, CancellationToken cancellationToken) => adminOverride
        ? dbContext.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId,
            cancellationToken)
        : dbContext.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId &&
            item.CashDrawer.SecretaryUserId == actorUserId, cancellationToken);

    private static IQueryable<CashWithdrawal> ApplyAccess(
        IQueryable<CashWithdrawal> query, long actorUserId,
        bool adminOverride) => adminOverride ? query : query.Where(item =>
            item.Shift.CashDrawer.SecretaryUserId == actorUserId);

    private IQueryable<CashWithdrawal> Details(bool tracking = false)
    {
        IQueryable<CashWithdrawal> query = tracking
            ? dbContext.CashWithdrawals
            : dbContext.CashWithdrawals.AsNoTracking();
        return query.Include(item => item.Shift).ThenInclude(item => item.CashDrawer);
    }

    private async Task<CashWithdrawalModel> MapAsync(CashWithdrawal withdrawal,
        CancellationToken cancellationToken) => (await MapManyAsync([withdrawal],
            cancellationToken)).Single();

    private async Task<IReadOnlyCollection<CashWithdrawalModel>> MapManyAsync(
        IReadOnlyCollection<CashWithdrawal> withdrawals,
        CancellationToken cancellationToken)
    {
        long[] userIds = withdrawals.SelectMany(item => new long?[]
            {
                item.RequestedByUserId,
                item.ReviewedByAdminUserId,
                item.ExecutedByUserId,
                item.CancelledByUserId
            }).Where(item => item.HasValue).Select(item => item!.Value)
            .Distinct().ToArray();
        Dictionary<long, string> names = await dbContext.Users.AsNoTracking()
            .Where(item => userIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.FullName,
                cancellationToken);
        return withdrawals.Select(item => new CashWithdrawalModel(
            item.Id,
            item.WithdrawalNumber,
            item.ShiftId,
            item.Amount,
            item.Reason,
            item.Status,
            item.RequestedByUserId,
            names[item.RequestedByUserId],
            item.RequestedAt,
            item.ReviewedByAdminUserId,
            item.ReviewedByAdminUserId.HasValue
                ? names[item.ReviewedByAdminUserId.Value] : null,
            item.ReviewedAt,
            item.DecisionReason,
            item.ExecutedByUserId,
            item.ExecutedByUserId.HasValue
                ? names[item.ExecutedByUserId.Value] : null,
            item.ExecutedAt,
            item.CancelledByUserId,
            item.CancelledByUserId.HasValue
                ? names[item.CancelledByUserId.Value] : null,
            item.CancelledAt,
            Convert.ToBase64String(item.RowVersion))).ToArray();
    }

    private async Task<string> NextWithdrawalNumberAsync(DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
        await using (command)
        {
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                "SELECT NEXT VALUE FOR [cashier].[CashWithdrawalNumberSequence]";
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            long sequence = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return $"WDL-{CashierInfrastructureSupport.ClinicDate(now):yyyyMMdd}-{sequence:D6}";
        }
    }

    private static string Fingerprint(long actorUserId, long shiftId,
        decimal amount, string reason)
    {
        string canonical = JsonSerializer.Serialize(new
        {
            ActorUserId = actorUserId,
            ShiftId = shiftId,
            Amount = amount,
            Reason = reason.Trim()
        });
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record ShiftTarget(long ShiftId, long SecretaryUserId);

    private sealed record WithdrawalTarget(long WithdrawalId, long ShiftId,
        long SecretaryUserId);
}
