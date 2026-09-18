namespace Clinic.Infrastructure.Cashier;

public sealed class ShiftService(ClinicDbContext dbContext, TimeProvider timeProvider)
    : IShiftService
{
    public async Task<Result<ShiftPolicyModel>> GetPolicyAsync(
        CancellationToken cancellationToken)
    {
        ShiftPolicy? policy = await dbContext.ShiftPolicies.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == ShiftPolicy.SingletonId,
                cancellationToken);
        return policy is null
            ? Result.Failure<ShiftPolicyModel>(CashierErrors.PolicyNotFound)
            : Result.Success(MapPolicy(policy));
    }

    public async Task<Result<ShiftPolicyModel>> UpdatePolicyAsync(long actorUserId,
        int closingGraceMinutes, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        ShiftPolicy? policy = await dbContext.ShiftPolicies.SingleOrDefaultAsync(
            item => item.Id == ShiftPolicy.SingletonId, cancellationToken);
        if (policy is null)
        {
            return Result.Failure<ShiftPolicyModel>(CashierErrors.PolicyNotFound);
        }

        if (!CashierInfrastructureSupport.MatchesVersion(policy.RowVersion, rowVersion))
        {
            return Result.Failure<ShiftPolicyModel>(CashierErrors.ConcurrencyConflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        policy.Update(closingGraceMinutes, actorUserId, now);
        dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
            actorUserId, "cashier.shift_policy_updated", nameof(ShiftPolicy),
            policy.Id, now, data: new { closingGraceMinutes }));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success(MapPolicy(policy));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ShiftPolicyModel>(CashierErrors.ConcurrencyConflict);
        }
    }

    public async Task<Result<CashDrawerPage>> ListDrawersAsync(int pageNumber,
        int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<CashDrawerDetails> query =
            from drawer in dbContext.CashDrawers.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on drawer.SecretaryUserId equals user.Id
            orderby user.FullName, drawer.Id
            select new CashDrawerDetails
            {
                Drawer = drawer,
                SecretaryName = user.FullName
            };

        int totalCount = await query.CountAsync(cancellationToken);
        List<CashDrawerModel> items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new CashDrawerModel(
                item.Drawer.Id,
                item.Drawer.SecretaryUserId,
                item.SecretaryName,
                item.Drawer.Name,
                item.Drawer.IsActive,
                Convert.ToBase64String(item.Drawer.RowVersion)))
            .ToListAsync(cancellationToken);
        return Result.Success(new CashDrawerPage(items, pageNumber, pageSize, totalCount));
    }

    public async Task<Result<GeneratedShifts>> GenerateAsync(long actorUserId,
        GenerateShiftsInput input, CancellationToken cancellationToken)
    {
        List<ShiftCandidate> candidates;
        try
        {
            candidates = CreateCandidates(input);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<GeneratedShifts>(
                CashierErrors.Validation(exception.Message));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (candidates.Count == 0 || candidates.Any(item => item.Start <= now))
        {
            return Result.Failure<GeneratedShifts>(CashierErrors.Validation(
                "يجب أن يحتوي الطلب على شيفت واحد على الأقل يبدأ في وقت لاحق."));
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireSecretaryAsync(dbContext,
                    input.SecretaryUserId, cancellationToken);

                DrawerIdentity? identity = await FindActiveDrawerAsync(
                    input.SecretaryUserId, cancellationToken);
                if (identity is null)
                {
                    return Result.Failure<GeneratedShifts>(CashierErrors.DrawerNotFound);
                }

                int graceMinutes = await dbContext.ShiftPolicies.AsNoTracking()
                    .Where(item => item.Id == ShiftPolicy.SingletonId)
                    .Select(item => item.ClosingGraceMinutes)
                    .SingleAsync(cancellationToken);
                candidates = candidates.Select(item => item with
                {
                    GraceEnd = item.End.AddMinutes(graceMinutes)
                }).ToList();

                DateTimeOffset windowStart = candidates.Min(item => item.Start);
                DateTimeOffset windowEnd = candidates.Max(item => item.GraceEnd);
                List<ShiftConflictModel> existing = await dbContext.Shifts.AsNoTracking()
                    .Where(item => item.CashDrawerId == identity.DrawerId &&
                        item.Status != ShiftStatus.Cancelled &&
                        item.ScheduledStart < windowEnd && windowStart < item.GraceEndsAt)
                    .Select(item => new ShiftConflictModel(
                        item.Id, item.ScheduledStart, item.GraceEndsAt))
                    .ToListAsync(cancellationToken);
                List<ShiftConflictModel> conflicts = existing.Where(item =>
                    candidates.Any(candidate => item.ScheduledStart < candidate.GraceEnd &&
                        candidate.Start < item.GraceEndsAt)).ToList();
                if (conflicts.Count != 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<GeneratedShifts>(CashierErrors.Conflict(
                        "توجد شيفتات متعارضة مع الفترة المطلوبة.", conflicts));
                }

                Shift[] shifts = candidates.Select(candidate => Shift.Create(
                    identity.DrawerId,
                    candidate.Start,
                    candidate.End,
                    graceMinutes,
                    actorUserId)).ToArray();
                dbContext.Shifts.AddRange(shifts);
                await dbContext.SaveChangesAsync(cancellationToken);
                foreach (Shift shift in shifts)
                {
                    dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
                        actorUserId, "cashier.shift_created", nameof(Shift),
                        shift.Id, now));
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                ShiftModel[] items = shifts.Select(shift =>
                    CashierInfrastructureSupport.Map(shift, input.SecretaryUserId,
                        identity.SecretaryName, now, adminCapabilities: true)).ToArray();
                return Result.Success(new GeneratedShifts(items.Length, items));
            });
        }
        catch (DbUpdateException)
        {
            return Result.Failure<GeneratedShifts>(CashierErrors.Conflict(
                "تعذر إنشاء الشيفتات بسبب تعارض متزامن."));
        }
    }

    public async Task<Result<ShiftModel>> UpdateAsync(long actorUserId,
        long shiftId, UpdateShiftInput input, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        DateTimeOffset start;
        DateTimeOffset end;
        try
        {
            start = CashierInfrastructureSupport.ToUtc(input.Date, input.StartTime);
            end = CashierInfrastructureSupport.ToUtc(input.Date, input.EndTime);
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<ShiftModel>(CashierErrors.Validation(exception.Message));
        }

        return await MutateScheduleAsync(actorUserId, shiftId, rowVersion,
            async (shift, now, token) =>
            {
                int graceMinutes = await dbContext.ShiftPolicies.AsNoTracking()
                    .Where(item => item.Id == ShiftPolicy.SingletonId)
                    .Select(item => item.ClosingGraceMinutes)
                    .SingleAsync(token);
                shift.UpdateSchedule(start, end, graceMinutes, now);
            }, "cashier.shift_updated", reason: null, cancellationToken);
    }

    public Task<Result<ShiftModel>> ExtendAsync(long actorUserId, long shiftId,
        DateTimeOffset newScheduledEnd, string reason, byte[] rowVersion,
        CancellationToken cancellationToken) => MutateScheduleAsync(
            actorUserId,
            shiftId,
            rowVersion,
            (shift, now, _) =>
            {
                DateTimeOffset end = newScheduledEnd.ToUniversalTime();
                if (CashierInfrastructureSupport.ClinicDate(end) !=
                    CashierInfrastructureSupport.ClinicDate(shift.ScheduledStart))
                {
                    throw new DomainException(
                        "يجب أن ينتهي الشيفت في يوم العيادة نفسه.");
                }

                shift.Extend(end, actorUserId, now);
                return Task.CompletedTask;
            },
            "cashier.shift_extended",
            reason,
            cancellationToken);

    public async Task<Result> CancelAsync(long actorUserId, long shiftId,
        string reason, byte[] rowVersion, CancellationToken cancellationToken)
    {
        Result<ShiftModel> result = await MutateScheduleAsync(actorUserId,
            shiftId, rowVersion, (shift, now, _) =>
            {
                shift.Cancel(actorUserId, now, reason);
                return Task.CompletedTask;
            }, "cashier.shift_cancelled", reason, cancellationToken,
            checkOverlap: false);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }

    public Task<Result<ShiftModel>> RecordOpeningBalanceAsync(long actorUserId,
        long shiftId, decimal amount, bool adminOverride, string? reason,
        byte[] rowVersion, CancellationToken cancellationToken) => MutateOwnedShiftAsync(
            actorUserId, shiftId, rowVersion, adminOverride, reason,
            (shift, now, _) =>
            {
                shift.RecordOpeningBalance(amount, actorUserId, now);
                return Task.CompletedTask;
            },
            "cashier.opening_balance_recorded",
            _ => new { amount },
            cancellationToken,
            requirePreviousClosed: true);

    public Task<Result<ShiftModel>> ReconcileAsync(long actorUserId, long shiftId,
        decimal declaredCash, bool adminOverride, string? reason,
        byte[] rowVersion, CancellationToken cancellationToken) => MutateOwnedShiftAsync(
            actorUserId, shiftId, rowVersion, adminOverride, reason,
            async (shift, now, token) =>
            {
                if (!adminOverride && now < shift.ScheduledEnd)
                {
                    throw new DomainException(
                        "لا يمكن إجراء المطابقة قبل موعد نهاية الشيفت.");
                }

                decimal expectedCash = await ExpectedCashAsync(shift, token);
                shift.Reconcile(expectedCash, declaredCash, actorUserId, now);
            },
            "cashier.shift_reconciled",
            shift => new
            {
                shift.ExpectedCash,
                shift.DeclaredCash,
                shift.CashVariance
            },
            cancellationToken);

    public async Task<Result<ShiftModel>> CloseAsync(long actorUserId, long shiftId,
        bool adminOverride, string? reason, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        return await MutateOwnedShiftAsync(actorUserId, shiftId, rowVersion,
            adminOverride, reason, async (shift, now, token) =>
            {
                if (await dbContext.CashWithdrawals.AsNoTracking().AnyAsync(item =>
                    item.ShiftId == shift.Id &&
                    (item.Status == CashWithdrawalStatus.Pending ||
                     item.Status == CashWithdrawalStatus.Approved), token))
                {
                    throw new DomainException(
                        "يجب حسم طلبات السحب المعلقة قبل إغلاق الشيفت.");
                }

                decimal expectedCash = await ExpectedCashAsync(shift, token);
                if (shift.ExpectedCash != expectedCash)
                {
                    throw new StaleReconciliationException();
                }

                shift.Close(expectedCash, actorUserId, now, adminOverride, reason);
            }, "cashier.shift_closed", shift => new
            {
                shift.ExpectedCash,
                shift.DeclaredCash,
                shift.CashVariance
            }, cancellationToken);
    }

    public async Task<Result<ShiftModel>> GetAsync(long shiftId,
        CancellationToken cancellationToken)
    {
        ShiftDetails? details = await ShiftDetailsQuery()
            .SingleOrDefaultAsync(item => item.Shift.Id == shiftId, cancellationToken);
        return details is null
            ? Result.Failure<ShiftModel>(CashierErrors.ShiftNotFound)
            : Result.Success(Map(details, adminCapabilities: true));
    }

    public async Task<Result<ShiftModel>> GetCurrentAsync(long actorUserId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        ShiftDetails? details = await ShiftDetailsQuery()
            .Where(item => item.SecretaryUserId == actorUserId &&
                item.Shift.Status != ShiftStatus.Closed &&
                item.Shift.Status != ShiftStatus.Cancelled)
            .OrderBy(item => item.Shift.ScheduledStart > now)
            .ThenBy(item => item.Shift.ScheduledStart)
            .FirstOrDefaultAsync(cancellationToken);
        return details is null
            ? Result.Failure<ShiftModel>(CashierErrors.ShiftNotFound)
            : Result.Success(Map(details, adminCapabilities: false));
    }

    public async Task<Result<ShiftPage>> SearchAsync(ShiftSearch search,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        IQueryable<ShiftDetails> query = ShiftDetailsQuery();
        if (search.SecretaryUserId.HasValue)
        {
            query = query.Where(item =>
                item.SecretaryUserId == search.SecretaryUserId.Value);
        }

        if (search.FromDate.HasValue)
        {
            DateTimeOffset from = CashierInfrastructureSupport.ClinicDayStartUtc(
                search.FromDate.Value);
            query = query.Where(item => item.Shift.ScheduledStart >= from);
        }

        if (search.ToDate.HasValue)
        {
            DateTimeOffset to = CashierInfrastructureSupport.ClinicDayStartUtc(
                search.ToDate.Value.AddDays(1));
            query = query.Where(item => item.Shift.ScheduledStart < to);
        }

        query = ApplyStatus(query, search.Status, now);
        int totalCount = await query.CountAsync(cancellationToken);
        List<ShiftDetails> rows = await query
            .OrderByDescending(item => item.Shift.ScheduledStart)
            .ThenByDescending(item => item.Shift.Id)
            .Skip((search.PageNumber - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);
        return Result.Success(new ShiftPage(rows.Select(item =>
                Map(item, adminCapabilities: true)).ToArray(),
            search.PageNumber, search.PageSize, totalCount));
    }

    public async Task<Result<ShiftPage>> HistoryAsync(long actorUserId,
        int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        IQueryable<ShiftDetails> query = ShiftDetailsQuery()
            .Where(item => item.SecretaryUserId == actorUserId &&
                item.Shift.ScheduledStart < now);
        int totalCount = await query.CountAsync(cancellationToken);
        List<ShiftDetails> rows = await query
            .OrderByDescending(item => item.Shift.ScheduledStart)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return Result.Success(new ShiftPage(rows.Select(item =>
                Map(item, adminCapabilities: false)).ToArray(),
            pageNumber, pageSize, totalCount));
    }

    private async Task<Result<ShiftModel>> MutateScheduleAsync(long actorUserId,
        long shiftId, byte[] rowVersion,
        Func<Shift, DateTimeOffset, CancellationToken, Task> mutation,
        string auditAction, string? reason, CancellationToken cancellationToken,
        bool checkOverlap = true)
    {
        ShiftLockTarget? target = await dbContext.Shifts.AsNoTracking()
            .Where(item => item.Id == shiftId)
            .Select(item => new ShiftLockTarget(item.CashDrawer.SecretaryUserId))
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null)
        {
            return Result.Failure<ShiftModel>(CashierErrors.ShiftNotFound);
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireSecretaryAsync(dbContext,
                    target.SecretaryUserId, cancellationToken);
                Shift? shift = await dbContext.Shifts.SingleOrDefaultAsync(
                    item => item.Id == shiftId, cancellationToken);
                if (shift is null)
                {
                    return Result.Failure<ShiftModel>(CashierErrors.ShiftNotFound);
                }

                if (!CashierInfrastructureSupport.MatchesVersion(shift.RowVersion, rowVersion))
                {
                    return Result.Failure<ShiftModel>(CashierErrors.ConcurrencyConflict);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                await mutation(shift, now, cancellationToken);
                if (checkOverlap)
                {
                    List<ShiftConflictModel> conflicts = await FindConflictsAsync(
                        shift.CashDrawerId, shift.Id, shift.ScheduledStart,
                        shift.GraceEndsAt, cancellationToken);
                    if (conflicts.Count != 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result.Failure<ShiftModel>(CashierErrors.Conflict(
                            "يتعارض وقت الشيفت مع شيفت آخر للسكرتيرة.", conflicts));
                    }
                }

                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
                    actorUserId, auditAction, nameof(Shift), shift.Id, now, reason));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                string secretaryName = await SecretaryNameAsync(
                    target.SecretaryUserId, cancellationToken);
                return Result.Success(CashierInfrastructureSupport.Map(
                    shift, target.SecretaryUserId, secretaryName, now,
                    adminCapabilities: true));
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<ShiftModel>(CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ShiftModel>(CashierErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<ShiftModel>(CashierErrors.Conflict(
                "تعذر حفظ الشيفت بسبب تعارض متزامن."));
        }
    }

    private async Task<Result<ShiftModel>> MutateOwnedShiftAsync(long actorUserId,
        long shiftId, byte[] rowVersion, bool adminOverride, string? reason,
        Func<Shift, DateTimeOffset, CancellationToken, Task> mutation, string auditAction,
        Func<Shift, object?>? auditData,
        CancellationToken cancellationToken,
        bool requirePreviousClosed = false)
    {
        ShiftLockTarget? target = await dbContext.Shifts.AsNoTracking()
            .Where(item => item.Id == shiftId)
            .Select(item => new ShiftLockTarget(item.CashDrawer.SecretaryUserId))
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null)
        {
            return Result.Failure<ShiftModel>(CashierErrors.ShiftNotFound);
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireSecretaryAsync(dbContext,
                    target.SecretaryUserId, cancellationToken);
                Shift? shift = await dbContext.Shifts.Include(item => item.CashDrawer)
                    .SingleOrDefaultAsync(item => item.Id == shiftId, cancellationToken);
                if (shift is null)
                {
                    return Result.Failure<ShiftModel>(CashierErrors.ShiftNotFound);
                }

                if (!adminOverride && shift.CashDrawer.SecretaryUserId != actorUserId)
                {
                    return Result.Failure<ShiftModel>(CashierErrors.Forbidden);
                }

                if (!CashierInfrastructureSupport.MatchesVersion(shift.RowVersion, rowVersion))
                {
                    return Result.Failure<ShiftModel>(CashierErrors.ConcurrencyConflict);
                }

                if (requirePreviousClosed && await dbContext.Shifts.AsNoTracking()
                    .AnyAsync(item => item.CashDrawerId == shift.CashDrawerId &&
                        item.Id != shift.Id &&
                        item.Status != ShiftStatus.Closed &&
                        item.Status != ShiftStatus.Cancelled &&
                        item.ScheduledStart < shift.ScheduledStart,
                        cancellationToken))
                {
                    return Result.Failure<ShiftModel>(CashierErrors.Conflict(
                        "يجب مطابقة وإغلاق الشيفت السابق قبل فتح الشيفت الحالي."));
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                await mutation(shift, now, cancellationToken);
                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(
                    actorUserId, auditAction, nameof(Shift), shift.Id, now, reason,
                    auditData?.Invoke(shift)));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                string secretaryName = await SecretaryNameAsync(
                    shift.CashDrawer.SecretaryUserId, cancellationToken);
                return Result.Success(CashierInfrastructureSupport.Map(shift,
                    shift.CashDrawer.SecretaryUserId, secretaryName, now,
                    adminOverride));
            });
        }
        catch (StaleReconciliationException)
        {
            return Result.Failure<ShiftModel>(CashierErrors.ReconciliationStale);
        }
        catch (DomainException exception)
        {
            return Result.Failure<ShiftModel>(CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ShiftModel>(CashierErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<ShiftModel>(CashierErrors.Conflict(
                "تعذر حفظ بيانات الشيفت بسبب تعارض متزامن."));
        }
    }

    private async Task<decimal> ExpectedCashAsync(Shift shift,
        CancellationToken cancellationToken) =>
        await CashierInfrastructureSupport.ExpectedCashAsync(dbContext,
            shift.Id, shift.OpeningBalance, cancellationToken);

    private IQueryable<ShiftDetails> ShiftDetailsQuery() =>
        from shift in dbContext.Shifts.AsNoTracking()
        join drawer in dbContext.CashDrawers.AsNoTracking()
            on shift.CashDrawerId equals drawer.Id
        join user in dbContext.Users.AsNoTracking()
            on drawer.SecretaryUserId equals user.Id
        select new ShiftDetails
        {
            Shift = shift,
            SecretaryUserId = drawer.SecretaryUserId,
            SecretaryName = user.FullName
        };

    private static IQueryable<ShiftDetails> ApplyStatus(
        IQueryable<ShiftDetails> query, ShiftStatus? status, DateTimeOffset now)
    {
        if (!status.HasValue)
        {
            return query;
        }

        return status.Value switch
        {
            ShiftStatus.Closed => query.Where(item =>
                item.Shift.Status == ShiftStatus.Closed),
            ShiftStatus.Cancelled => query.Where(item =>
                item.Shift.Status == ShiftStatus.Cancelled),
            ShiftStatus.Scheduled => query.Where(item =>
                item.Shift.Status != ShiftStatus.Closed &&
                item.Shift.Status != ShiftStatus.Cancelled &&
                now < item.Shift.ScheduledStart),
            ShiftStatus.Open => query.Where(item =>
                item.Shift.Status != ShiftStatus.Closed &&
                item.Shift.Status != ShiftStatus.Cancelled &&
                item.Shift.ScheduledStart <= now && now < item.Shift.ScheduledEnd),
            ShiftStatus.Grace => query.Where(item =>
                item.Shift.Status != ShiftStatus.Closed &&
                item.Shift.Status != ShiftStatus.Cancelled &&
                item.Shift.ScheduledEnd <= now),
            _ => query
        };
    }

    private async Task<DrawerIdentity?> FindActiveDrawerAsync(long secretaryUserId,
        CancellationToken cancellationToken) => await (
        from drawer in dbContext.CashDrawers
        join user in dbContext.Users on drawer.SecretaryUserId equals user.Id
        join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
        join role in dbContext.Roles on userRole.RoleId equals role.Id
        where drawer.SecretaryUserId == secretaryUserId && drawer.IsActive &&
            !user.IsDisabled && !user.IsArchived && role.Name == RoleNames.Secretary
        select new DrawerIdentity(drawer.Id, user.FullName)).SingleOrDefaultAsync(
            cancellationToken);

    private Task<string> SecretaryNameAsync(long secretaryUserId,
        CancellationToken cancellationToken) => dbContext.Users.AsNoTracking()
        .Where(item => item.Id == secretaryUserId)
        .Select(item => item.FullName)
        .SingleAsync(cancellationToken);

    private Task<List<ShiftConflictModel>> FindConflictsAsync(long drawerId,
        long excludedShiftId, DateTimeOffset start, DateTimeOffset graceEnd,
        CancellationToken cancellationToken) => dbContext.Shifts.AsNoTracking()
        .Where(item => item.CashDrawerId == drawerId && item.Id != excludedShiftId &&
            item.Status != ShiftStatus.Cancelled &&
            item.ScheduledStart < graceEnd && start < item.GraceEndsAt)
        .Select(item => new ShiftConflictModel(
            item.Id, item.ScheduledStart, item.GraceEndsAt))
        .ToListAsync(cancellationToken);

    private static List<ShiftCandidate> CreateCandidates(GenerateShiftsInput input)
    {
        HashSet<ClinicDayOfWeek> days = [.. input.DaysOfWeek];
        List<ShiftCandidate> candidates = [];
        for (DateOnly date = input.FromDate; ; date = date.AddDays(1))
        {
            ClinicDayOfWeek clinicDay = (ClinicDayOfWeek)(
                ((int)date.DayOfWeek + 6) % 7 + 1);
            if (days.Contains(clinicDay))
            {
                DateTimeOffset start = CashierInfrastructureSupport.ToUtc(
                    date, input.StartTime);
                DateTimeOffset end = CashierInfrastructureSupport.ToUtc(
                    date, input.EndTime);
                candidates.Add(new ShiftCandidate(start, end, end));
            }

            if (date == input.ToDate)
            {
                break;
            }
        }

        return candidates;
    }

    private ShiftModel Map(ShiftDetails details, bool adminCapabilities) =>
        CashierInfrastructureSupport.Map(
        details.Shift, details.SecretaryUserId, details.SecretaryName,
        timeProvider.GetUtcNow(), adminCapabilities);

    private static ShiftPolicyModel MapPolicy(ShiftPolicy policy) => new(
        policy.ClosingGraceMinutes, Convert.ToBase64String(policy.RowVersion));

    private sealed class CashDrawerDetails
    {
        public required CashDrawer Drawer { get; init; }
        public required string SecretaryName { get; init; }
    }
    private sealed record DrawerIdentity(long DrawerId, string SecretaryName);
    private sealed record ShiftCandidate(DateTimeOffset Start, DateTimeOffset End,
        DateTimeOffset GraceEnd);
    private sealed class ShiftDetails
    {
        public required Shift Shift { get; init; }
        public long SecretaryUserId { get; init; }
        public required string SecretaryName { get; init; }
    }
    private sealed record ShiftLockTarget(long SecretaryUserId);
    private sealed class StaleReconciliationException : Exception;
}
