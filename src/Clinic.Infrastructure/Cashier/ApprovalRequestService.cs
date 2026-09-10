using System.Data;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Common;
using Clinic.Application.Features.Cashier;
using Clinic.Domain.Appointments;
using Clinic.Domain.Approvals;
using Clinic.Domain.Auditing;
using Clinic.Domain.Cashier;
using Clinic.Domain.Common;
using Clinic.Infrastructure.Appointments;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Cashier;

public sealed class ApprovalRequestService(
    ClinicDbContext dbContext,
    TimeProvider timeProvider) : IApprovalRequestService
{
    public async Task<Result<ApprovalRequestModel>> CreateCancellationAsync(
        long actorUserId, long appointmentId, string reason,
        byte[] appointmentRowVersion, CancellationToken cancellationToken)
    {
        ApprovalTarget? target = await FindAppointmentTargetAsync(appointmentId,
            cancellationToken);
        if (target is null)
        {
            return Result.Failure<ApprovalRequestModel>(
                CashierErrors.ApprovalRequestNotFound);
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable,
                        cancellationToken);
                await AcquireTargetLocksAsync(target, includeRequest: false,
                    cancellationToken);

                Appointment? appointment = await dbContext.Appointments
                    .SingleOrDefaultAsync(item => item.Id == appointmentId,
                        cancellationToken);
                if (appointment is null)
                {
                    return Result.Failure<ApprovalRequestModel>(
                        CashierErrors.ApprovalRequestNotFound);
                }

                if (!CashierInfrastructureSupport.MatchesVersion(
                    appointment.RowVersion, appointmentRowVersion))
                {
                    return Result.Failure<ApprovalRequestModel>(
                        CashierErrors.ConcurrencyConflict);
                }

                if (!NeedsApproval(appointment) ||
                    await dbContext.ApprovalRequests.AnyAsync(item =>
                        item.AppointmentId == appointmentId &&
                        (item.Status == ApprovalRequestStatus.Pending ||
                         item.Status == ApprovalRequestStatus.Approved),
                        cancellationToken))
                {
                    return Result.Failure<ApprovalRequestModel>(CashierErrors.Conflict(
                        "هذا الحجز لا يقبل طلب إلغاء جديد."));
                }

                PaymentTarget? payment = await GetPaymentTargetAsync(appointment,
                    cancellationToken);
                if (appointment.PaymentStatus == PaymentStatus.Paid && payment is null ||
                    appointment.PaymentStatus != PaymentStatus.Paid &&
                    appointment.PaymentStatus != PaymentStatus.Unpaid &&
                    appointment.PaymentStatus != PaymentStatus.CoveredByPackage &&
                    appointment.PaymentStatus != PaymentStatus.NotRequired)
                {
                    return Result.Failure<ApprovalRequestModel>(CashierErrors.Conflict(
                        "الحالة المالية للحجز لا تسمح بطلب الإلغاء."));
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                ApprovalRequest request = ApprovalRequest.CreateAppointmentCancellation(
                    appointment.Id, payment?.PaymentId, payment?.Amount,
                    actorUserId, now, reason);
                dbContext.ApprovalRequests.Add(request);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(actorUserId,
                    "cashier.cancellation_requested", nameof(ApprovalRequest),
                    request.Id, now, reason,
                    new { request.AppointmentId, request.OriginalPaymentId,
                        request.RequestedAmount }));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(await MapOneAsync(request.Id,
                    cancellationToken));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ApprovalRequestModel>(
                CashierErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (
            CashierInfrastructureSupport.IsUniqueViolation(exception,
                "UX_ApprovalRequests_ActiveAppointment"))
        {
            return Result.Failure<ApprovalRequestModel>(CashierErrors.Conflict(
                "يوجد طلب إلغاء نشط لهذا الحجز بالفعل."));
        }
    }

    public Task<Result<ApprovalRequestModel>> ApproveAsync(long actorUserId,
        long requestId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken) => ReviewAsync(actorUserId, requestId,
            reason, rowVersion, approve: true, cancellationToken);

    public Task<Result<ApprovalRequestModel>> RejectAsync(long actorUserId,
        long requestId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken) => ReviewAsync(actorUserId, requestId,
            reason, rowVersion, approve: false, cancellationToken);

    public async Task<Result<ApprovalRequestModel>> GetAsync(long requestId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ApprovalRequests.AsNoTracking()
            .AnyAsync(item => item.Id == requestId, cancellationToken))
        {
            return Result.Failure<ApprovalRequestModel>(
                CashierErrors.ApprovalRequestNotFound);
        }

        return Result.Success(await MapOneAsync(requestId, cancellationToken));
    }

    public async Task<Result<ApprovalRequestPage>> SearchAsync(
        ApprovalRequestSearch search, CancellationToken cancellationToken)
    {
        IQueryable<ApprovalRequest> query = dbContext.ApprovalRequests.AsNoTracking();
        if (search.Status.HasValue)
        {
            query = query.Where(item => item.Status == search.Status.Value);
        }

        if (search.AwaitingRefundOnly)
        {
            query = query.Where(item => item.Status == ApprovalRequestStatus.Approved &&
                item.RequestedAmount != null && !dbContext.Refunds.Any(refund =>
                    refund.ApprovalRequestId == item.Id));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        long[] ids = await query.OrderByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.Id)
            .Skip((search.PageNumber - 1) * search.PageSize)
            .Take(search.PageSize).Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        IReadOnlyCollection<ApprovalRequestModel> items = await MapManyAsync(ids,
            cancellationToken);
        Dictionary<long, int> positions = ids.Select((id, index) => (id, index))
            .ToDictionary(item => item.id, item => item.index);
        ApprovalRequestModel[] orderedItems = items
            .OrderBy(item => positions[item.Id]).ToArray();
        return Result.Success(new ApprovalRequestPage(orderedItems, search.PageNumber,
            search.PageSize, totalCount));
    }

    private async Task<Result<ApprovalRequestModel>> ReviewAsync(long actorUserId,
        long requestId, string reason, byte[] rowVersion, bool approve,
        CancellationToken cancellationToken)
    {
        ApprovalTarget? target = await FindRequestTargetAsync(requestId,
            cancellationToken);
        if (target is null)
        {
            return Result.Failure<ApprovalRequestModel>(
                CashierErrors.ApprovalRequestNotFound);
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable,
                        cancellationToken);
                await AcquireTargetLocksAsync(target, includeRequest: true,
                    cancellationToken);

                ApprovalRequest? request = await dbContext.ApprovalRequests
                    .SingleOrDefaultAsync(item => item.Id == requestId,
                        cancellationToken);
                if (request is null)
                {
                    return Result.Failure<ApprovalRequestModel>(
                        CashierErrors.ApprovalRequestNotFound);
                }

                if (!CashierInfrastructureSupport.MatchesVersion(request.RowVersion,
                    rowVersion))
                {
                    return Result.Failure<ApprovalRequestModel>(
                        CashierErrors.ConcurrencyConflict);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                if (approve)
                {
                    Appointment? appointment = await dbContext.Appointments
                        .Include(item => item.Services)
                        .SingleOrDefaultAsync(item => item.Id == request.AppointmentId,
                            cancellationToken);
                    if (appointment is null || !NeedsApproval(appointment) ||
                        !await RequestPaymentStillValidAsync(request, appointment,
                            cancellationToken))
                    {
                        return Result.Failure<ApprovalRequestModel>(
                            CashierErrors.Conflict(
                                "تغير الحجز أو التحصيل منذ إنشاء الطلب."));
                    }

                    request.Approve(actorUserId, now, reason);
                    if (appointment.PatientPackageId.HasValue)
                    {
                        await PackageSessionLifecycle.ReleaseAsync(dbContext,
                            appointment.Id, actorUserId, now, cancellationToken);
                    }
                    appointment.CancelAfterApproval(actorUserId, now, reason);
                    AppointmentInfrastructureSupport.AddAudit(dbContext, actorUserId,
                        "appointments.cancellation_approved", appointment.Id, now,
                        reason);
                }
                else
                {
                    request.Reject(actorUserId, now, reason);
                }

                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(actorUserId,
                    approve ? "cashier.cancellation_approved" :
                        "cashier.cancellation_rejected",
                    nameof(ApprovalRequest), request.Id, now, reason));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(await MapOneAsync(request.Id,
                    cancellationToken));
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<ApprovalRequestModel>(
                CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ApprovalRequestModel>(
                CashierErrors.ConcurrencyConflict);
        }
    }

    private async Task<bool> RequestPaymentStillValidAsync(ApprovalRequest request,
        Appointment appointment, CancellationToken cancellationToken)
    {
        if (!request.OriginalPaymentId.HasValue)
        {
            return appointment.PaymentStatus is PaymentStatus.Unpaid or
                PaymentStatus.CoveredByPackage or PaymentStatus.NotRequired;
        }

        if (appointment.PaymentStatus != PaymentStatus.Paid)
        {
            return false;
        }

        return await dbContext.AppointmentPaymentAllocations.AsNoTracking()
            .AnyAsync(item => item.AppointmentId == appointment.Id &&
                item.PaymentId == request.OriginalPaymentId &&
                item.Amount == request.RequestedAmount,
                cancellationToken);
    }

    private async Task<PaymentTarget?> GetPaymentTargetAsync(Appointment appointment,
        CancellationToken cancellationToken)
    {
        if (appointment.PaymentStatus != PaymentStatus.Paid)
        {
            return null;
        }

        return await dbContext.AppointmentPaymentAllocations.AsNoTracking()
            .Where(item => item.AppointmentId == appointment.Id)
            .Select(item => new PaymentTarget(item.PaymentId, item.Amount))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static bool NeedsApproval(Appointment appointment) =>
        appointment.Status == AppointmentStatus.Confirmed ||
        appointment.Status == AppointmentStatus.Suspended &&
        appointment.StatusBeforeSuspension == AppointmentStatus.Confirmed;

    private async Task AcquireTargetLocksAsync(ApprovalTarget target,
        bool includeRequest, CancellationToken cancellationToken)
    {
        await TransactionalResourceLock.AcquireDepartmentAsync(dbContext,
            target.DepartmentId, cancellationToken);
        await TransactionalResourceLock.AcquirePatientAsync(dbContext,
            target.PatientId, cancellationToken);
        await TransactionalResourceLock.AcquireAppointmentAsync(dbContext,
            target.AppointmentId, cancellationToken);
        if (target.PatientPackageId.HasValue)
        {
            await TransactionalResourceLock.AcquirePatientPackageAsync(dbContext,
                target.PatientPackageId.Value, cancellationToken);
        }
        if (target.PaymentId.HasValue)
        {
            await TransactionalResourceLock.AcquirePaymentAsync(dbContext,
                target.PaymentId.Value, cancellationToken);
        }

        if (includeRequest)
        {
            await TransactionalResourceLock.AcquireApprovalRequestAsync(dbContext,
                target.RequestId!.Value, cancellationToken);
        }
    }

    private Task<ApprovalTarget?> FindAppointmentTargetAsync(long appointmentId,
        CancellationToken cancellationToken) => dbContext.Appointments.AsNoTracking()
        .Where(item => item.Id == appointmentId)
        .Select(item => new ApprovalTarget(null, item.Id, item.PatientId,
            item.DepartmentId, item.PatientPackageId,
            dbContext.AppointmentPaymentAllocations
                .Where(allocation => allocation.AppointmentId == item.Id)
                .Select(allocation => (long?)allocation.PaymentId).SingleOrDefault()))
        .SingleOrDefaultAsync(cancellationToken);

    private Task<ApprovalTarget?> FindRequestTargetAsync(long requestId,
        CancellationToken cancellationToken) =>
        (from request in dbContext.ApprovalRequests.AsNoTracking()
         join appointment in dbContext.Appointments.AsNoTracking()
             on request.AppointmentId equals appointment.Id
         where request.Id == requestId
         select new ApprovalTarget(request.Id, appointment.Id,
             appointment.PatientId, appointment.DepartmentId,
             appointment.PatientPackageId, request.OriginalPaymentId))
        .SingleOrDefaultAsync(cancellationToken);

    private async Task<ApprovalRequestModel> MapOneAsync(long requestId,
        CancellationToken cancellationToken) =>
        (await MapManyAsync([requestId], cancellationToken)).Single();

    private async Task<IReadOnlyCollection<ApprovalRequestModel>> MapManyAsync(
        IReadOnlyCollection<long> requestIds, CancellationToken cancellationToken)
    {
        ApprovalProjection[] rows = await (
            from request in dbContext.ApprovalRequests.AsNoTracking()
            join appointment in dbContext.Appointments.AsNoTracking()
                on request.AppointmentId equals appointment.Id
            join patient in dbContext.Patients.AsNoTracking()
                on appointment.PatientId equals patient.Id
            join requester in dbContext.Users.AsNoTracking()
                on request.RequestedByUserId equals requester.Id
            join reviewerCandidate in dbContext.Users.AsNoTracking()
                on request.ReviewedByAdminUserId equals reviewerCandidate.Id
                into reviewers
            from reviewer in reviewers.DefaultIfEmpty()
            join paymentCandidate in dbContext.Payments.AsNoTracking()
                on request.OriginalPaymentId equals paymentCandidate.Id into payments
            from payment in payments.DefaultIfEmpty()
            join refundCandidate in dbContext.Refunds.AsNoTracking()
                on request.Id equals refundCandidate.ApprovalRequestId into refunds
            from refund in refunds.DefaultIfEmpty()
            where requestIds.Contains(request.Id)
            select new ApprovalProjection(request, appointment.PatientId,
                patient.FullName, requester.FullName,
                reviewer == null ? null : reviewer.FullName,
                payment == null ? null : payment.TransactionNumber,
                refund == null ? null : refund.Id)).ToArrayAsync(cancellationToken);

        long[] paymentIds = rows.Where(item => item.Request.OriginalPaymentId.HasValue)
            .Select(item => item.Request.OriginalPaymentId!.Value).Distinct().ToArray();
        PaymentMethodProjection[] methodRows = await dbContext.PaymentMethodAllocations
            .AsNoTracking().Where(item => paymentIds.Contains(item.PaymentId))
            .Select(item => new PaymentMethodProjection(item.PaymentId, item.Id,
                item.PaymentMethodId, item.PaymentMethod.Code,
                item.PaymentMethod.DisplayName, item.PaymentMethod.IsCash, item.Amount))
            .ToArrayAsync(cancellationToken);
        Dictionary<long, decimal> refundedByMethod = await dbContext
            .RefundMethodAllocations.AsNoTracking()
            .Where(item => paymentIds.Contains(item.OriginalPaymentId) &&
                item.Refund.Status == RefundStatus.Posted)
            .GroupBy(item => item.OriginalAllocationId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Amount),
                cancellationToken);

        return rows.Select(row => new ApprovalRequestModel(row.Request.Id,
            row.Request.RequestType, row.Request.AppointmentId, row.PatientId,
            row.PatientName, row.Request.OriginalPaymentId, row.PaymentNumber,
            row.Request.RequestedAmount, row.Request.RequestedByUserId,
            row.RequesterName, row.Request.RequestedAt, row.Request.RequestNote,
            row.Request.Status, row.Request.ReviewedByAdminUserId,
            row.ReviewerName, row.Request.ReviewedAt, row.Request.DecisionReason,
            row.Request.Status == ApprovalRequestStatus.Approved &&
                row.Request.RequestedAmount.HasValue && !row.RefundId.HasValue,
            row.RefundId, methodRows.Where(item =>
                item.PaymentId == row.Request.OriginalPaymentId)
                .Select(item => new RefundableMethodModel(item.AllocationId,
                    item.PaymentMethodId, item.Code, item.DisplayName, item.IsCash,
                    item.Amount - refundedByMethod.GetValueOrDefault(item.AllocationId)))
                .Where(item => item.RemainingAmount > 0).ToArray(),
            Convert.ToBase64String(row.Request.RowVersion))).ToArray();
    }

    private sealed record ApprovalTarget(long? RequestId, long AppointmentId,
        long PatientId, long DepartmentId, long? PatientPackageId, long? PaymentId);
    private sealed record PaymentTarget(long PaymentId, decimal Amount);
    private sealed record ApprovalProjection(ApprovalRequest Request, long PatientId,
        string PatientName, string RequesterName, string? ReviewerName,
        string? PaymentNumber, long? RefundId);
    private sealed record PaymentMethodProjection(long PaymentId, long AllocationId,
        long PaymentMethodId, string Code, string DisplayName, bool IsCash,
        decimal Amount);
}
