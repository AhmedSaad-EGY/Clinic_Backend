using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

public sealed class PaymentService(ClinicDbContext dbContext, TimeProvider timeProvider)
    : IPaymentService
{
    public async Task<Result<IReadOnlyCollection<PaymentMethodModel>>> ListMethodsAsync(
        CancellationToken cancellationToken)
    {
        PaymentMethodModel[] methods = await dbContext.PaymentMethods.AsNoTracking()
            .Where(item => item.IsActive).OrderBy(item => item.SortOrder)
            .Select(item => new PaymentMethodModel(item.Id, item.Code,
                item.DisplayName, item.IsCash, !item.IsCash, item.SortOrder))
            .ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<PaymentMethodModel>>(methods);
    }

    public async Task<Result<PostedPaymentModel>> PostAsync(long actorUserId,
        Guid idempotencyKey, PostPaymentInput input, bool adminOverride,
        CancellationToken cancellationToken)
    {
        Payment? existing = await PaymentDetails().SingleOrDefaultAsync(item =>
            item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(existing, actorUserId, input, adminOverride,
                cancellationToken);
        }

        DateTimeOffset requestTime = timeProvider.GetUtcNow();
        ShiftTarget? target = await FindShiftTargetAsync(actorUserId, input.ShiftId,
            adminOverride, requestTime, cancellationToken);
        PaymentTarget? paymentTarget = await FindPaymentTargetAsync(input.AppointmentIds,
            cancellationToken);
        if (target is null)
        {
            return Result.Failure<PostedPaymentModel>(CashierErrors.ShiftNotFound);
        }

        if (paymentTarget is null)
        {
            return Result.Failure<PostedPaymentModel>(
                CashierErrors.AppointmentNotPayable);
        }

        string fingerprint = Fingerprint(actorUserId, target.ShiftId, input,
            adminOverride);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquireSecretaryAsync(dbContext,
                    target.SecretaryUserId, cancellationToken);
                await TransactionalResourceLock.AcquireShiftAsync(dbContext,
                    target.ShiftId, cancellationToken);
                await TransactionalResourceLock.AcquirePatientAsync(dbContext,
                    paymentTarget.PatientId, cancellationToken);
                foreach (long appointmentId in paymentTarget.AppointmentIds)
                {
                    await TransactionalResourceLock.AcquireAppointmentAsync(dbContext,
                        appointmentId, cancellationToken);
                }
                DateTimeOffset now = timeProvider.GetUtcNow();

                Payment? replay = await PaymentDetails(tracking: true)
                    .SingleOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey,
                        cancellationToken);
                if (replay is not null)
                {
                    return await ReplayAsync(replay, actorUserId, input, adminOverride,
                        cancellationToken);
                }

                Shift? shift = await dbContext.Shifts.Include(item => item.CashDrawer)
                    .SingleOrDefaultAsync(item => item.Id == target.ShiftId,
                        cancellationToken);
                if (shift is null)
                {
                    return Result.Failure<PostedPaymentModel>(CashierErrors.ShiftNotFound);
                }

                if (shift.CashDrawer.SecretaryUserId != target.SecretaryUserId ||
                    (!adminOverride && target.SecretaryUserId != actorUserId))
                {
                    return Result.Failure<PostedPaymentModel>(CashierErrors.Forbidden);
                }

                Appointment[] appointments = await dbContext.Appointments
                    .Where(item => input.AppointmentIds.Contains(item.Id))
                    .OrderBy(item => item.Id).ToArrayAsync(cancellationToken);
                if (appointments.Length != input.AppointmentIds.Count ||
                    appointments.Select(item => item.PatientId).Distinct().Count() != 1)
                {
                    return Result.Failure<PostedPaymentModel>(
                        CashierErrors.AppointmentNotPayable);
                }

                long patientId = paymentTarget.PatientId;
                if (appointments.Any(item => item.PaymentStatus !=
                        Clinic.Domain.Appointments.PaymentStatus.Unpaid ||
                    item.NetAmount <= 0 || item.Status is AppointmentStatus.Suspended or
                        AppointmentStatus.Cancelled or AppointmentStatus.NoShow))
                {
                    return Result.Failure<PostedPaymentModel>(
                        CashierErrors.AppointmentNotPayable);
                }

                if (await dbContext.ApprovalRequests.AnyAsync(item =>
                    input.AppointmentIds.Contains(item.AppointmentId) &&
                    (item.Status == ApprovalRequestStatus.Pending ||
                     item.Status == ApprovalRequestStatus.Approved),
                    cancellationToken))
                {
                    return Result.Failure<PostedPaymentModel>(CashierErrors.Conflict(
                        "يوجد طلب إلغاء نشط لأحد الحجوزات."));
                }

                long[] methodIds = input.MethodAllocations
                    .Select(item => item.PaymentMethodId).ToArray();
                Dictionary<long, PaymentMethod> methods = await dbContext.PaymentMethods
                    .Where(item => methodIds.Contains(item.Id) && item.IsActive)
                    .ToDictionaryAsync(item => item.Id, cancellationToken);
                if (methods.Count != methodIds.Length)
                {
                    return Result.Failure<PostedPaymentModel>(
                        CashierErrors.PaymentMethodNotFound);
                }

                shift.RegisterCollection(now);
                foreach (Appointment appointment in appointments)
                {
                    appointment.RecordFullPayment(actorUserId, now);
                }

                string transactionNumber = await NextTransactionNumberAsync(now,
                    cancellationToken);
                Payment payment = Payment.Create(transactionNumber, idempotencyKey,
                    fingerprint, shift.Id, patientId, target.SecretaryUserId, now,
                    input.Note, input.MethodAllocations.Select(item =>
                    {
                        PaymentMethod method = methods[item.PaymentMethodId];
                        return (method.Id, method.IsCash, item.Amount,
                            item.ReferenceNumber);
                    }).ToArray(), appointments.Select(item =>
                        (item.Id, item.NetAmount)).ToArray());
                dbContext.Payments.Add(payment);
                await dbContext.SaveChangesAsync(cancellationToken);

                dbContext.AuditLogs.Add(CashierInfrastructureSupport.Audit(actorUserId,
                    "cashier.payment_posted", nameof(Payment), payment.Id, now,
                    adminOverride ? input.Reason?.Trim() : null,
                    new { payment.ShiftId, payment.TotalAmount,
                        AppointmentIds = appointments.Select(item => item.Id).ToArray() }));
                foreach (Appointment appointment in appointments)
                {
                    AppointmentInfrastructureSupport.AddAudit(dbContext, actorUserId,
                        "appointments.payment_recorded", appointment.Id, now);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                Payment saved = await PaymentDetails().SingleAsync(item =>
                    item.Id == payment.Id, cancellationToken);
                return Result.Success(new PostedPaymentModel(
                    await MapAsync(saved, cancellationToken), false));
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<PostedPaymentModel>(
                CashierErrors.Conflict(exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PostedPaymentModel>(CashierErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (CashierInfrastructureSupport.IsUniqueViolation(exception,
            "UX_Payments_IdempotencyKey"))
        {
            dbContext.ChangeTracker.Clear();
            Payment? replay = await PaymentDetails().SingleOrDefaultAsync(item =>
                item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (replay is null)
            {
                throw;
            }

            return await ReplayAsync(replay, actorUserId, input, adminOverride,
                cancellationToken);
        }
        catch (DbUpdateException exception) when (CashierInfrastructureSupport.IsUniqueViolation(exception,
            "UX_AppointmentPaymentAllocations_AppointmentId"))
        {
            return Result.Failure<PostedPaymentModel>(
                CashierErrors.AppointmentNotPayable);
        }
    }

    public async Task<Result<PaymentModel>> GetAsync(long actorUserId, long paymentId,
        bool adminOverride, CancellationToken cancellationToken)
    {
        Payment? payment = await ApplyAccess(PaymentDetails(), actorUserId, adminOverride)
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
        return payment is null
            ? Result.Failure<PaymentModel>(CashierErrors.PaymentNotFound)
            : Result.Success(await MapAsync(payment, cancellationToken));
    }

    public async Task<Result<PaymentPage>> ListForShiftAsync(long actorUserId,
        long shiftId, bool adminOverride, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessShiftAsync(actorUserId, shiftId, adminOverride,
            cancellationToken))
        {
            return Result.Failure<PaymentPage>(CashierErrors.Forbidden);
        }

        IQueryable<Payment> query = PaymentDetails().Where(item => item.ShiftId == shiftId);
        int totalCount = await query.CountAsync(cancellationToken);
        Payment[] payments = await query.OrderByDescending(item => item.CollectedAt)
            .ThenByDescending(item => item.Id).Skip((pageNumber - 1) * pageSize)
            .Take(pageSize).ToArrayAsync(cancellationToken);
        long[] collectorIds = payments.Select(item => item.CollectedByUserId)
            .Distinct().ToArray();
        Dictionary<long, string> collectorNames = await dbContext.Users.AsNoTracking()
            .Where(item => collectorIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.FullName,
                cancellationToken);
        PaymentModel[] models = payments.Select(item =>
            Map(item, collectorNames[item.CollectedByUserId])).ToArray();

        return Result.Success(new PaymentPage(models, pageNumber, pageSize, totalCount));
    }

    public async Task<Result<ShiftCollectionSummaryModel>> GetShiftSummaryAsync(
        long actorUserId, long shiftId, bool adminOverride,
        CancellationToken cancellationToken)
    {
        Shift? shift = await dbContext.Shifts.AsNoTracking().Include(item => item.CashDrawer)
            .SingleOrDefaultAsync(item => item.Id == shiftId, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<ShiftCollectionSummaryModel>(CashierErrors.ShiftNotFound);
        }

        if (!adminOverride && shift.CashDrawer.SecretaryUserId != actorUserId)
        {
            return Result.Failure<ShiftCollectionSummaryModel>(CashierErrors.Forbidden);
        }

        PaymentMethod[] methods = await dbContext.PaymentMethods.AsNoTracking()
            .Where(item => item.IsActive).OrderBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        Dictionary<long, decimal> totals = await dbContext.PaymentMethodAllocations
            .AsNoTracking().Where(item => item.Payment.ShiftId == shiftId)
            .GroupBy(item => item.PaymentMethodId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Amount),
                cancellationToken);
        Dictionary<long, decimal> refunds = await dbContext.RefundMethodAllocations
            .AsNoTracking().Where(item => item.Refund.ExecutionShiftId == shiftId &&
                item.Refund.Status == RefundStatus.Posted)
            .GroupBy(item => item.OriginalAllocation.PaymentMethodId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Amount),
                cancellationToken);
        PaymentMethodTotalModel[] methodTotals = methods.Select(item =>
            new PaymentMethodTotalModel(item.Id, item.Code, item.DisplayName, item.IsCash,
                totals.GetValueOrDefault(item.Id), refunds.GetValueOrDefault(item.Id),
                totals.GetValueOrDefault(item.Id) - refunds.GetValueOrDefault(item.Id)))
            .ToArray();
        decimal cash = methodTotals.Where(item => item.IsCash).Sum(item => item.Amount);
        decimal cashRefunded = methodTotals.Where(item => item.IsCash)
            .Sum(item => item.RefundedAmount);
        decimal electronic = methodTotals.Where(item => !item.IsCash).Sum(item => item.Amount);
        decimal electronicRefunded = methodTotals.Where(item => !item.IsCash)
            .Sum(item => item.RefundedAmount);
        decimal? expected = shift.OpeningBalance.HasValue
            ? shift.OpeningBalance.Value + cash - cashRefunded
            : null;
        int paymentCount = await dbContext.Payments.AsNoTracking()
            .CountAsync(item => item.ShiftId == shiftId, cancellationToken);
        int refundCount = await dbContext.Refunds.AsNoTracking()
            .CountAsync(item => item.ExecutionShiftId == shiftId &&
                item.Status == RefundStatus.Posted, cancellationToken);
        return Result.Success(new ShiftCollectionSummaryModel(shift.Id,
            shift.OpeningBalance, cash, cashRefunded, cash - cashRefunded,
            electronic, electronicRefunded, electronic - electronicRefunded,
            cash + electronic, cashRefunded + electronicRefunded,
            cash + electronic - cashRefunded - electronicRefunded, expected,
            shift.ExpectedCash, shift.ExpectedCash.HasValue && shift.ExpectedCash != expected,
            expected < 0, paymentCount, refundCount, methodTotals));
    }

    private async Task<Result<PostedPaymentModel>> ReplayAsync(Payment payment,
        long actorUserId, PostPaymentInput input, bool adminOverride,
        CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprint(actorUserId, input.ShiftId ?? payment.ShiftId,
            input, adminOverride);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(payment.RequestFingerprint),
            Encoding.ASCII.GetBytes(fingerprint)))
        {
            return Result.Failure<PostedPaymentModel>(CashierErrors.IdempotencyConflict);
        }

        return Result.Success(new PostedPaymentModel(
            await MapAsync(payment, cancellationToken), true));
    }

    private async Task<ShiftTarget?> FindShiftTargetAsync(long actorUserId,
        long? requestedShiftId, bool adminOverride, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IQueryable<Shift> query = dbContext.Shifts.AsNoTracking()
            .Where(item => item.Status != ShiftStatus.Closed &&
                item.Status != ShiftStatus.Cancelled && item.ScheduledStart <= now &&
                now < item.GraceEndsAt);
        query = adminOverride
            ? query.Where(item => item.Id == requestedShiftId)
            : query.Where(item => item.CashDrawer.SecretaryUserId == actorUserId);
        return await query.Select(item => new ShiftTarget(item.Id,
            item.CashDrawer.SecretaryUserId)).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<PaymentTarget?> FindPaymentTargetAsync(
        IReadOnlyCollection<long> appointmentIds,
        CancellationToken cancellationToken)
    {
        AppointmentIdentity[] appointments = await dbContext.Appointments.AsNoTracking()
            .Where(item => appointmentIds.Contains(item.Id))
            .Select(item => new AppointmentIdentity(item.Id, item.PatientId))
            .ToArrayAsync(cancellationToken);
        return appointments.Length == appointmentIds.Count &&
            appointments.Select(item => item.PatientId).Distinct().Count() == 1
            ? new PaymentTarget(appointments[0].PatientId,
                appointments.Select(item => item.AppointmentId).Order().ToArray())
            : null;
    }

    private Task<bool> CanAccessShiftAsync(long actorUserId, long shiftId,
        bool adminOverride, CancellationToken cancellationToken) => adminOverride
        ? dbContext.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId,
            cancellationToken)
        : dbContext.Shifts.AsNoTracking().AnyAsync(item => item.Id == shiftId &&
            item.CashDrawer.SecretaryUserId == actorUserId, cancellationToken);

    private static IQueryable<Payment> ApplyAccess(IQueryable<Payment> query,
        long actorUserId, bool adminOverride) => adminOverride
        ? query
        : query.Where(item => item.Shift.CashDrawer.SecretaryUserId == actorUserId);

    private IQueryable<Payment> PaymentDetails(bool tracking = false)
    {
        IQueryable<Payment> query = tracking
            ? dbContext.Payments
            : dbContext.Payments.AsNoTracking();
        return query.Include(item => item.Patient)
            .Include(item => item.Shift).ThenInclude(item => item.CashDrawer)
            .Include(item => item.MethodAllocations).ThenInclude(item => item.PaymentMethod)
            .Include(item => item.AppointmentAllocations);
    }

    private async Task<PaymentModel> MapAsync(Payment payment,
        CancellationToken cancellationToken)
    {
        string collectorName = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == payment.CollectedByUserId)
            .Select(item => item.FullName).SingleAsync(cancellationToken);
        return Map(payment, collectorName);
    }

    private static PaymentModel Map(Payment payment, string collectorName) =>
        new(payment.Id, payment.TransactionNumber, payment.ShiftId,
            payment.PatientId, payment.Patient.FullName, payment.TotalAmount,
            payment.CollectedByUserId, collectorName, payment.CollectedAt, payment.Status,
            payment.Note, payment.MethodAllocations.OrderBy(item =>
                item.PaymentMethod.SortOrder).Select(item => new PaymentMethodAllocationModel(
                    item.PaymentMethodId, item.PaymentMethod.Code,
                    item.PaymentMethod.DisplayName, item.PaymentMethod.IsCash, item.Amount,
                    item.ReferenceNumber)).ToArray(), payment.AppointmentAllocations
                .OrderBy(item => item.AppointmentId)
                .Select(item => new AppointmentPaymentAllocationModel(item.AppointmentId,
                    item.Amount)).ToArray(), Convert.ToBase64String(payment.RowVersion));

    private async Task<string> NextTransactionNumberAsync(DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DbCommand command = dbContext.Database.GetDbConnection().CreateCommand();
        await using (command)
        {
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                "SELECT NEXT VALUE FOR [cashier].[PaymentTransactionNumberSequence]";
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            long sequence = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return $"PAY-{CashierInfrastructureSupport.ClinicDate(now):yyyyMMdd}-{sequence:D6}";
        }
    }

    private static string Fingerprint(long actorUserId, long shiftId,
        PostPaymentInput input, bool adminOverride)
    {
        string canonical = JsonSerializer.Serialize(new
        {
            ActorUserId = actorUserId,
            ShiftId = shiftId,
            AdminOverride = adminOverride,
            AppointmentIds = input.AppointmentIds.Order().ToArray(),
            Methods = input.MethodAllocations.OrderBy(item => item.PaymentMethodId)
                .Select(item => new
                {
                    item.PaymentMethodId,
                    item.Amount,
                    ReferenceNumber = item.ReferenceNumber?.Trim()
                }).ToArray(),
            Note = input.Note?.Trim(),
            Reason = input.Reason?.Trim()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record ShiftTarget(long ShiftId, long SecretaryUserId);
    private sealed record PaymentTarget(long PatientId, long[] AppointmentIds);
    private sealed record AppointmentIdentity(long AppointmentId, long PatientId);
}
