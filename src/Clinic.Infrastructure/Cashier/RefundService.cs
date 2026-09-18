namespace Clinic.Infrastructure.Cashier;

public sealed class RefundService(ClinicDbContext dbContext, TimeProvider timeProvider)
    : IRefundService
{
    public async Task<Result<PostedRefundModel>> ExecuteAsync(long actorUserId,
        Guid idempotencyKey, ExecuteRefundInput input,
        CancellationToken cancellationToken)
    {
        Refund? existing = await RefundDetails().SingleOrDefaultAsync(item =>
            item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(existing, actorUserId, input,
                cancellationToken);
        }

        DateTimeOffset requestTime = timeProvider.GetUtcNow();
        ShiftTarget? shiftTarget = await FindCurrentShiftAsync(actorUserId,
            requestTime, cancellationToken);
        RefundTarget? refundTarget = await FindRefundTargetAsync(
            input.ApprovalRequestId, cancellationToken);
        if (shiftTarget is null)
        {
            return Result.Failure<PostedRefundModel>(CashierErrors.ShiftNotFound);
        }

        if (refundTarget is null)
        {
            return Result.Failure<PostedRefundModel>(
                CashierErrors.ApprovalRequestNotFound);
        }

        string fingerprint = Fingerprint(actorUserId, shiftTarget.ShiftId, input);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable,
                        cancellationToken);
                await AcquireLocksAsync(shiftTarget, refundTarget,
                    cancellationToken);
                DateTimeOffset now = timeProvider.GetUtcNow();

                Refund? replay = await RefundDetails(tracking: true)
                    .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey,
                        cancellationToken);
                if (replay is not null)
                {
                    return await ReplayAsync(replay, actorUserId, input,
                        cancellationToken);
                }

                Shift? shift = await dbContext.Shifts.Include(item => item.CashDrawer)
                    .SingleOrDefaultAsync(item => item.Id == shiftTarget.ShiftId,
                        cancellationToken);
                ApprovalRequest? request = await dbContext.ApprovalRequests
                    .SingleOrDefaultAsync(item => item.Id == input.ApprovalRequestId,
                        cancellationToken);
                AppointmentPaymentAllocation? appointmentAllocation = await dbContext
                    .AppointmentPaymentAllocations.Include(item => item.Appointment)
                    .Include(item => item.Payment)
                    .SingleOrDefaultAsync(item =>
                        item.AppointmentId == refundTarget.AppointmentId &&
                        item.PaymentId == refundTarget.PaymentId,
                        cancellationToken);
                if (shift is null || shift.CashDrawer.SecretaryUserId != actorUserId)
                {
                    return Result.Failure<PostedRefundModel>(CashierErrors.Forbidden);
                }

                if (request is null || request.Status != ApprovalRequestStatus.Approved ||
                    request.OriginalPaymentId != refundTarget.PaymentId ||
                    request.RequestedAmount is null || appointmentAllocation is null ||
                    request.RequestedAmount != appointmentAllocation.Amount ||
                    await dbContext.Refunds.AnyAsync(item =>
                        item.ApprovalRequestId == request.Id, cancellationToken))
                {
                    return Result.Failure<PostedRefundModel>(CashierErrors.Conflict(
                        "طلب الاسترداد غير معتمد أو تم تنفيذه بالفعل."));
                }

                long[] allocationIds = input.MethodAllocations
                    .Select(item => item.OriginalAllocationId).ToArray();
                PaymentMethodAllocation[] originalMethods = await dbContext
                    .PaymentMethodAllocations.Include(item => item.PaymentMethod)
                    .Where(item => item.PaymentId == refundTarget.PaymentId &&
                        allocationIds.Contains(item.Id)).ToArrayAsync(cancellationToken);
                if (originalMethods.Length != allocationIds.Length)
                {
                    return Result.Failure<PostedRefundModel>(
                        CashierErrors.PaymentMethodNotFound);
                }

                Dictionary<long, decimal> refundedByMethod = await dbContext
                    .RefundMethodAllocations.Where(item =>
                        allocationIds.Contains(item.OriginalAllocationId) &&
                        item.Refund.Status == RefundStatus.Posted)
                    .GroupBy(item => item.OriginalAllocationId)
                    .ToDictionaryAsync(group => group.Key,
                        group => group.Sum(item => item.Amount), cancellationToken);
                Dictionary<long, PaymentMethodAllocation> methodMap = originalMethods
                    .ToDictionary(item => item.Id);
                if (input.MethodAllocations.Sum(item => item.Amount) !=
                        request.RequestedAmount.Value ||
                    input.MethodAllocations.Any(item => item.Amount >
                        methodMap[item.OriginalAllocationId].Amount -
                        refundedByMethod.GetValueOrDefault(item.OriginalAllocationId)))
                {
                    return Result.Failure<PostedRefundModel>(CashierErrors.Conflict(
                        "توزيع الاسترداد يتجاوز الرصيد المتاح أو لا يساوي المبلغ المطلوب."));
                }

                shift.RegisterRefund(now);
                string transactionNumber = await NextTransactionNumberAsync(now,
                    cancellationToken);
                Refund refund = Refund.Create(transactionNumber, idempotencyKey,
                    fingerprint, request.Id, refundTarget.PaymentId, shift.Id,
                    actorUserId, now, input.Note, input.MethodAllocations.Select(item =>
                    {
                        PaymentMethodAllocation original =
                            methodMap[item.OriginalAllocationId];
                        return (original.Id, original.PaymentMethod.IsCash,
                            item.Amount, item.ReferenceNumber);
                    }).ToArray(), appointmentAllocation.Id,
                    request.RequestedAmount.Value);

                decimal previousRefunds = await dbContext.RefundAppointmentAllocations
                    .Where(item => item.OriginalPaymentId == refundTarget.PaymentId &&
                        item.Refund.Status == RefundStatus.Posted)
                    .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0;
                appointmentAllocation.Payment.RecordRefund(previousRefunds + refund.Amount);
                appointmentAllocation.Appointment.RecordFullRefund(actorUserId, now);
                dbContext.Refunds.Add(refund);
                await dbContext.SaveChangesAsync(cancellationToken);

                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(actorUserId,
                    "cashier.refund_posted", nameof(Refund), refund.Id, now,
                    request.DecisionReason, new
                    {
                        refund.ApprovalRequestId,
                        refund.OriginalPaymentId,
                        refund.ExecutionShiftId,
                        refund.Amount,
                        refundTarget.AppointmentId
                    }));
                AppointmentInfrastructureSupport.AddAudit(dbContext, actorUserId,
                    "appointments.refund_recorded", refundTarget.AppointmentId, now,
                    request.DecisionReason);
                await dbContext.SaveChangesAsync(cancellationToken);

                decimal expectedCash = await ExpectedCashAsync(shift,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                Refund saved = await RefundDetails().SingleAsync(item =>
                    item.Id == refund.Id, cancellationToken);
                return Result.Success(new PostedRefundModel(
                    await MapAsync(saved, cancellationToken), false, expectedCash,
                    expectedCash < 0));
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<PostedRefundModel>(
                CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PostedRefundModel>(
                CashierErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (CashierInfrastructureSupport.IsUniqueViolation(exception,
            "UX_Refunds_IdempotencyKey"))
        {
            dbContext.ChangeTracker.Clear();
            Refund? replay = await RefundDetails().SingleOrDefaultAsync(item =>
                item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return await ReplayAsync(replay, actorUserId, input, cancellationToken);
        }
        catch (DbUpdateException exception) when (
            CashierInfrastructureSupport.IsUniqueViolation(exception, "UX_Refunds_ApprovalRequestId") ||
            CashierInfrastructureSupport.IsUniqueViolation(exception,
                "UX_RefundAppointmentAllocations_OriginalAllocationId"))
        {
            return Result.Failure<PostedRefundModel>(CashierErrors.Conflict(
                "تم تنفيذ استرداد هذا الحجز بالفعل."));
        }
    }

    public async Task<Result<RefundModel>> GetAsync(long actorUserId, long refundId,
        bool adminOverride, CancellationToken cancellationToken)
    {
        Refund? refund = await ApplyAccess(RefundDetails(), actorUserId,
            adminOverride).SingleOrDefaultAsync(item => item.Id == refundId,
                cancellationToken);
        return refund is null
            ? Result.Failure<RefundModel>(CashierErrors.RefundNotFound)
            : Result.Success(await MapAsync(refund, cancellationToken));
    }

    public async Task<Result<RefundModel>> GetForPatientAsync(long patientId,
        long refundId, CancellationToken cancellationToken)
    {
        Refund? refund = await RefundDetails().SingleOrDefaultAsync(item =>
            item.Id == refundId && item.AppointmentAllocations.Any(allocation =>
                allocation.OriginalAllocation.PatientId == patientId), cancellationToken);
        return refund is null
            ? Result.Failure<RefundModel>(CashierErrors.RefundNotFound)
            : Result.Success(await MapAsync(refund, cancellationToken));
    }

    public async Task<Result<RefundPage>> ListForShiftAsync(long actorUserId,
        long shiftId, bool adminOverride, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessShiftAsync(actorUserId, shiftId, adminOverride,
            cancellationToken))
        {
            return Result.Failure<RefundPage>(CashierErrors.Forbidden);
        }

        IQueryable<Refund> query = RefundDetails().Where(item =>
            item.ExecutionShiftId == shiftId);
        int totalCount = await query.CountAsync(cancellationToken);
        Refund[] refunds = await query.OrderByDescending(item => item.ExecutedAt)
            .ThenByDescending(item => item.Id).Skip((pageNumber - 1) * pageSize)
            .Take(pageSize).ToArrayAsync(cancellationToken);
        RefundModel[] models = await MapManyAsync(refunds, cancellationToken);
        return Result.Success(new RefundPage(models, pageNumber, pageSize,
            totalCount));
    }

    private async Task<Result<PostedRefundModel>> ReplayAsync(Refund refund,
        long actorUserId, ExecuteRefundInput input,
        CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint(actorUserId, refund.ExecutionShiftId, input);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(refund.RequestFingerprint),
            Encoding.ASCII.GetBytes(fingerprint)))
        {
            return Result.Failure<PostedRefundModel>(
                CashierErrors.IdempotencyConflict);
        }

        Shift shift = await dbContext.Shifts.AsNoTracking().SingleAsync(item =>
            item.Id == refund.ExecutionShiftId, cancellationToken);
        decimal expectedCash = await ExpectedCashAsync(shift, cancellationToken);
        return Result.Success(new PostedRefundModel(
            await MapAsync(refund, cancellationToken), true, expectedCash,
            expectedCash < 0));
    }

    private async Task AcquireLocksAsync(ShiftTarget shift, RefundTarget refund,
        CancellationToken cancellationToken)
    {
        await TransactionalResourceLock.AcquireSecretaryAsync(dbContext,
            shift.SecretaryUserId, cancellationToken);
        await TransactionalResourceLock.AcquireShiftAsync(dbContext, shift.ShiftId,
            cancellationToken);
        await TransactionalResourceLock.AcquirePatientAsync(dbContext,
            refund.PatientId, cancellationToken);
        await TransactionalResourceLock.AcquireAppointmentAsync(dbContext,
            refund.AppointmentId, cancellationToken);
        await TransactionalResourceLock.AcquirePaymentAsync(dbContext,
            refund.PaymentId, cancellationToken);
        await TransactionalResourceLock.AcquireApprovalRequestAsync(dbContext,
            refund.RequestId, cancellationToken);
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

    private Task<RefundTarget?> FindRefundTargetAsync(long requestId,
        CancellationToken cancellationToken) =>
        (from request in dbContext.ApprovalRequests.AsNoTracking()
         join appointment in dbContext.Appointments.AsNoTracking()
             on request.AppointmentId equals appointment.Id
         where request.Id == requestId && request.OriginalPaymentId != null
         select new RefundTarget(request.Id, appointment.Id,
             appointment.PatientId, request.OriginalPaymentId!.Value))
        .SingleOrDefaultAsync(cancellationToken);

    private async Task<decimal> ExpectedCashAsync(Shift shift,
        CancellationToken cancellationToken) =>
        await CashierInfrastructureSupport.ExpectedCashAsync(dbContext,
            shift.Id, shift.OpeningBalance, cancellationToken);

    private Task<bool> CanAccessShiftAsync(long actorUserId, long shiftId,
        bool adminOverride, CancellationToken cancellationToken) => adminOverride
        ? dbContext.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId,
            cancellationToken)
        : dbContext.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId &&
            item.CashDrawer.SecretaryUserId == actorUserId, cancellationToken);

    private static IQueryable<Refund> ApplyAccess(IQueryable<Refund> query,
        long actorUserId, bool adminOverride) => adminOverride ? query :
        query.Where(item => item.ExecutedByUserId == actorUserId);

    private IQueryable<Refund> RefundDetails(bool tracking = false)
    {
        IQueryable<Refund> query = tracking ? dbContext.Refunds :
            dbContext.Refunds.AsNoTracking();
        return query.Include(item => item.MethodAllocations)
            .ThenInclude(item => item.OriginalAllocation)
            .ThenInclude(item => item.PaymentMethod)
            .Include(item => item.AppointmentAllocations)
            .ThenInclude(item => item.OriginalAllocation)
            .AsSplitQuery();
    }

    private async Task<RefundModel> MapAsync(Refund refund,
        CancellationToken cancellationToken) =>
        (await MapManyAsync([refund], cancellationToken)).Single();

    private async Task<RefundModel[]> MapManyAsync(IReadOnlyCollection<Refund> refunds,
        CancellationToken cancellationToken)
    {
        long[] userIds = refunds.Select(item => item.ExecutedByUserId)
            .Distinct().ToArray();
        Dictionary<long, string> users = await dbContext.Users.AsNoTracking()
            .Where(item => userIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.FullName,
                cancellationToken);
        return refunds.Select(refund => new RefundModel(refund.Id,
            refund.TransactionNumber, refund.ApprovalRequestId,
            refund.OriginalPaymentId,
            refund.AppointmentAllocations.Single().OriginalAllocation.AppointmentId,
            refund.ExecutionShiftId, refund.Amount, refund.ExecutedByUserId,
            users[refund.ExecutedByUserId], refund.ExecutedAt, refund.Status,
            refund.Note, refund.MethodAllocations.Select(item =>
                new RefundMethodAllocationModel(item.OriginalAllocationId,
                    item.OriginalAllocation.PaymentMethodId,
                    item.OriginalAllocation.PaymentMethod.Code,
                    item.OriginalAllocation.PaymentMethod.DisplayName,
                    item.OriginalAllocation.PaymentMethod.IsCash, item.Amount,
                    item.ReferenceNumber)).ToArray(),
            Convert.ToBase64String(refund.RowVersion))).ToArray();
    }

    private async Task<string> NextTransactionNumberAsync(DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
        await using (command)
        {
            command.Transaction = dbContext.Database.CurrentTransaction?
                .GetDbTransaction();
            command.CommandText =
                "SELECT NEXT VALUE FOR [cashier].[RefundTransactionNumberSequence]";
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            long sequence = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return $"REF-{CashierInfrastructureSupport.ClinicDate(now):yyyyMMdd}-{sequence:D6}";
        }
    }

    private static string Fingerprint(long actorUserId, long shiftId,
        ExecuteRefundInput input)
    {
        string canonical = JsonSerializer.Serialize(new
        {
            ActorUserId = actorUserId,
            ShiftId = shiftId,
            input.ApprovalRequestId,
            Methods = input.MethodAllocations
                .OrderBy(item => item.OriginalAllocationId)
                .Select(item => new
                {
                    item.OriginalAllocationId,
                    item.Amount,
                    ReferenceNumber = item.ReferenceNumber?.Trim()
                }).ToArray(),
            Note = input.Note?.Trim()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record ShiftTarget(long ShiftId, long SecretaryUserId);
    private sealed record RefundTarget(long RequestId, long AppointmentId,
        long PatientId, long PaymentId);
}
