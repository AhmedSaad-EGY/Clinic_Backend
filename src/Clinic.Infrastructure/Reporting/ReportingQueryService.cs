using System.Globalization;
using Clinic.Application.Abstractions.Reporting;
using Clinic.Application.Common;
using Clinic.Application.Features.Reporting;
using Clinic.Domain.Appointments;
using Clinic.Domain.Approvals;
using Clinic.Domain.Auditing;
using Clinic.Domain.Cashier;
using Clinic.Infrastructure.Cashier;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Reporting;

public sealed class ReportingQueryService(ClinicDbContext dbContext,
    TimeProvider timeProvider) : IReportingQueryService
{
    private const int MaximumFactRows = 100_000;

    public async Task<Result<AdminDashboardSummaryModel>> GetDashboardAsync(
        DateOnly? reportDate, CancellationToken cancellationToken)
    {
        DateOnly clinicDate = reportDate ?? CashierInfrastructureSupport.ClinicDate(
            timeProvider.GetUtcNow());
        (DateTimeOffset start, DateTimeOffset end) = Period(clinicDate, clinicDate);

        DashboardMethodFact[] collections = await dbContext
            .PaymentMethodAllocations.AsNoTracking()
            .Where(item => item.Payment.CollectedAt >= start &&
                item.Payment.CollectedAt < end)
            .Select(item => new DashboardMethodFact(item.Amount,
                item.PaymentMethod.IsCash)).Take(MaximumFactRows + 1)
            .ToArrayAsync(cancellationToken);
        DashboardMethodFact[] refunds = await dbContext
            .RefundMethodAllocations.AsNoTracking()
            .Where(item => item.Refund.ExecutedAt >= start &&
                item.Refund.ExecutedAt < end &&
                item.Refund.Status == RefundStatus.Posted)
            .Select(item => new DashboardMethodFact(item.Amount,
                item.OriginalAllocation.PaymentMethod.IsCash))
            .Take(MaximumFactRows + 1).ToArrayAsync(cancellationToken);
        DashboardAppointmentFact[] appointments = await dbContext.Appointments
            .AsNoTracking().Where(item => item.StartAt >= start && item.StartAt < end)
            .Select(item => new DashboardAppointmentFact(item.Status,
                item.PaymentStatus, item.NetAmount, item.PatientId,
                item.Patient.CreatedAt)).Take(MaximumFactRows + 1)
            .ToArrayAsync(cancellationToken);
        if (TooLarge(collections) || TooLarge(refunds) || TooLarge(appointments))
        {
            return Result.Failure<AdminDashboardSummaryModel>(
                ReportingErrors.ResultTooLarge);
        }

        decimal withdrawals = await dbContext.CashWithdrawals.AsNoTracking()
            .Where(item => item.ExecutedAt >= start && item.ExecutedAt < end &&
                item.Status == CashWithdrawalStatus.Executed)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0;
        int newPatients = await dbContext.Patients.AsNoTracking().CountAsync(item =>
            item.CreatedAt >= start && item.CreatedAt < end, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        int activeShifts = await dbContext.Shifts.AsNoTracking().CountAsync(item =>
            item.Status != ShiftStatus.Closed && item.Status != ShiftStatus.Cancelled &&
            item.ScheduledStart <= now && now < item.GraceEndsAt, cancellationToken);
        int varianceShifts = await dbContext.Shifts.AsNoTracking().CountAsync(item =>
            item.ClosedAt >= start && item.ClosedAt < end && item.CashVariance != 0,
            cancellationToken);
        int pendingApprovals = await dbContext.ApprovalRequests.AsNoTracking()
            .CountAsync(item => item.Status == ApprovalRequestStatus.Pending,
                cancellationToken);
        int pendingWithdrawals = await dbContext.CashWithdrawals.AsNoTracking()
            .CountAsync(item => item.Status == CashWithdrawalStatus.Pending ||
                item.Status == CashWithdrawalStatus.Approved, cancellationToken);

        decimal collected = collections.Sum(item => item.Amount);
        decimal cashCollected = collections.Where(item => item.IsCash)
            .Sum(item => item.Amount);
        decimal refunded = refunds.Sum(item => item.Amount);
        return Result.Success(new AdminDashboardSummaryModel(clinicDate, collected,
            cashCollected, collected - cashCollected, refunded, collected - refunded,
            withdrawals, appointments.Where(item =>
                item.Status == AppointmentStatus.Completed &&
                item.PaymentStatus == PaymentStatus.Unpaid).Sum(item => item.NetAmount),
            appointments.Length,
            Count(appointments, AppointmentStatus.Booked),
            Count(appointments, AppointmentStatus.Confirmed),
            Count(appointments, AppointmentStatus.Completed),
            Count(appointments, AppointmentStatus.NoShow),
            Count(appointments, AppointmentStatus.Cancelled),
            Count(appointments, AppointmentStatus.Suspended), newPatients,
            appointments.Where(item => item.PatientCreatedAt < start)
                .Select(item => item.PatientId).Distinct().Count(), activeShifts,
            varianceShifts, pendingApprovals, pendingWithdrawals));
    }

    public async Task<Result<FinancialReportModel>> GetFinancialAsync(
        FinancialReportFilter filter, CancellationToken cancellationToken)
    {
        Result<FinancialData> dataResult = await LoadFinancialAsync(filter,
            cancellationToken);
        if (dataResult.IsFailure)
        {
            return Result.Failure<FinancialReportModel>(dataResult.Error);
        }

        FinancialData data = dataResult.Value;
        Dictionary<long, decimal> collectionFactors = MethodFactors(
            data.CollectionMethods, filter.PaymentMethodId);
        Dictionary<long, decimal> refundFactors = RefundMethodFactors(
            data.RefundMethods, filter.PaymentMethodId);

        decimal AppointmentValue(FinancialFact fact) => fact.IsPackage
            ? 0 : fact.Amount * collectionFactors.GetValueOrDefault(fact.MovementId);
        decimal RefundValue(RefundFact fact) => fact.Amount *
            refundFactors.GetValueOrDefault(fact.MovementId);

        decimal collected = Money(data.Collections.Sum(item => item.Amount *
            collectionFactors.GetValueOrDefault(item.MovementId)));
        decimal refunded = Money(data.Refunds.Sum(RefundValue));
        FinancialBreakdownModel[] departments = BuildBreakdown(
            data.Collections.Select(item => new BreakdownShare(item.DimensionId,
                item.DimensionName, item.Amount * collectionFactors.GetValueOrDefault(
                    item.MovementId))),
            data.Refunds.Select(item => new BreakdownShare(item.DimensionId,
                item.DimensionName, RefundValue(item))), collected, refunded,
            orderByNet: true);
        FinancialBreakdownModel[] methods = MethodBreakdown(data,
            filter.PaymentMethodId, collected, refunded);
        decimal cashCollected = methods.Where(item => item.Id.HasValue &&
                data.CashMethodIds.Contains(item.Id.Value))
            .Sum(item => item.Collected);
        decimal cashWithdrawn = HasTargetDimension(filter) ? 0 : await dbContext
            .CashWithdrawals.AsNoTracking().Where(item =>
                item.Status == CashWithdrawalStatus.Executed &&
                item.ExecutedAt >= data.Start && item.ExecutedAt < data.End &&
                (!filter.ShiftId.HasValue || item.ShiftId == filter.ShiftId.Value) &&
                (!filter.SecretaryUserId.HasValue ||
                    item.ExecutedByUserId == filter.SecretaryUserId.Value))
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0;

        decimal appointmentRevenue = data.Collections.Any(item => !item.IsPackage)
            ? data.Collections.Any(item => item.IsPackage)
                ? Money(data.Collections.Where(item => !item.IsPackage)
                    .Sum(AppointmentValue)) : collected
            : 0;
        decimal packageRevenue = collected - appointmentRevenue;
        return Result.Success(new FinancialReportModel(filter.From, filter.To,
            Money(collected), Money(refunded), Money(collected - refunded),
            Money(cashCollected), Money(collected - cashCollected),
            Money(data.Collections.Sum(item => item.DiscountAmount *
                collectionFactors.GetValueOrDefault(item.MovementId))),
            appointmentRevenue, packageRevenue, cashWithdrawn, departments,
            methods));
    }

    public async Task<Result<OperationalReportModel>> GetOperationalAsync(
        OperationalReportFilter filter, CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = Period(filter.From, filter.To);
        IQueryable<Appointment> query = dbContext.Appointments.AsNoTracking().Where(item =>
            item.StartAt >= start && item.StartAt < end &&
            (!filter.DepartmentId.HasValue || item.DepartmentId == filter.DepartmentId) &&
            (!filter.Status.HasValue || item.Status == filter.Status) &&
            (!filter.Gender.HasValue || item.Patient.Gender == filter.Gender) &&
            (string.IsNullOrWhiteSpace(filter.Area) ||
                item.Patient.Area != null && item.Patient.Area.Contains(filter.Area.Trim())) &&
            (!filter.ServiceId.HasValue || item.Services.Any(line =>
                line.ServiceId == filter.ServiceId &&
                line.Status != AppointmentServiceStatus.Superseded)) &&
            (!filter.DoctorId.HasValue || item.Services.Any(line =>
                line.DoctorService.DoctorId == filter.DoctorId &&
                line.Status != AppointmentServiceStatus.Superseded)));
        OperationalAppointmentFact[] candidates = await query.Select(item =>
            new OperationalAppointmentFact(item.Id, item.PatientId, item.DepartmentId,
                item.Services.Where(line =>
                        line.Status != AppointmentServiceStatus.Superseded)
                    .Select(line => line.Service.Department.Name).First(),
                item.Status, item.Patient.CreatedAt,
                item.Patient.BirthDate, item.Patient.AgeAtRegistration,
                item.Patient.AgeRecordedAt)).Take(MaximumFactRows + 1)
            .ToArrayAsync(cancellationToken);
        if (TooLarge(candidates))
        {
            return Result.Failure<OperationalReportModel>(
                ReportingErrors.ResultTooLarge);
        }

        HashSet<long> appointmentIds = candidates.Where(item =>
            MatchesAge(item, filter.MinimumAge, filter.MaximumAge, filter.To))
            .Select(item => item.Id).ToHashSet();
        OperationalAppointmentFact[] appointments = candidates.Where(item =>
            appointmentIds.Contains(item.Id)).ToArray();

        OperationalServiceFact[] serviceCandidates = await query
            .SelectMany(item => item.Services.Where(line =>
                line.Status != AppointmentServiceStatus.Superseded &&
                (!filter.ServiceId.HasValue || line.ServiceId == filter.ServiceId) &&
                (!filter.DoctorId.HasValue ||
                    line.DoctorService.DoctorId == filter.DoctorId)),
                (appointment, line) => new OperationalServiceFact(appointment.Id,
                    appointment.DepartmentId, line.Service.Department.Name,
                    appointment.Status, line.Id, line.Quantity, line.NetAmount,
                    line.PackageCoveredAmount, line.DoctorService.DoctorId,
                    line.DoctorService.Doctor.Name, line.Devices.Count))
            .Take(MaximumFactRows + 1).ToArrayAsync(cancellationToken);
        if (TooLarge(serviceCandidates))
        {
            return Result.Failure<OperationalReportModel>(
                ReportingErrors.ResultTooLarge);
        }

        OperationalServiceFact[] services = serviceCandidates.Where(item =>
            appointmentIds.Contains(item.AppointmentId)).ToArray();
        int deviceUsage = services.Where(item =>
                item.AppointmentStatus == AppointmentStatus.Completed)
            .Sum(item => item.DeviceCount);

        OperationalBreakdownModel[] departments = appointments.GroupBy(item =>
            new { item.DepartmentId, item.DepartmentName }).Select(group =>
            new OperationalBreakdownModel(group.Key.DepartmentId,
                group.Key.DepartmentName, group.Count(), services.Where(item =>
                    item.DepartmentId == group.Key.DepartmentId).Sum(item => item.Quantity),
                Money(services.Where(item => item.DepartmentId == group.Key.DepartmentId &&
                    item.AppointmentStatus == AppointmentStatus.Completed)
                    .Sum(item => item.NetAmount + item.PackageCoveredAmount))))
            .OrderByDescending(item => item.AppointmentCount).ToArray();
        OperationalBreakdownModel[] doctors = services.GroupBy(item => new
            { item.DoctorId, item.DoctorName }).Select(group =>
            new OperationalBreakdownModel(group.Key.DoctorId, group.Key.DoctorName,
                group.Select(item => item.AppointmentId).Distinct().Count(),
                group.Sum(item => item.Quantity), Money(group.Where(item =>
                    item.AppointmentStatus == AppointmentStatus.Completed)
                    .Sum(item => item.NetAmount + item.PackageCoveredAmount))))
            .OrderByDescending(item => item.ServiceValue).ToArray();

        return Result.Success(new OperationalReportModel(filter.From, filter.To,
            appointments.Length, Count(appointments, AppointmentStatus.Booked),
            Count(appointments, AppointmentStatus.Confirmed),
            Count(appointments, AppointmentStatus.Completed),
            Count(appointments, AppointmentStatus.NoShow),
            Count(appointments, AppointmentStatus.Cancelled),
            Count(appointments, AppointmentStatus.Suspended),
            services.Sum(item => item.Quantity), services.Count(item =>
                item.AppointmentStatus == AppointmentStatus.Completed &&
                item.PackageCoveredAmount > 0), deviceUsage,
            appointments.Where(item => item.PatientCreatedAt >= start &&
                item.PatientCreatedAt < end).Select(item => item.PatientId).Distinct().Count(),
            appointments.Where(item => item.PatientCreatedAt < start)
                .Select(item => item.PatientId).Distinct().Count(), departments, doctors));
    }

    public async Task<Result<ComparisonReportModel>> GetComparisonAsync(
        ComparisonReportFilter filter, CancellationToken cancellationToken)
    {
        FinancialReportFilter financialFilter = new(filter.From, filter.To,
            filter.DepartmentId, null, filter.DoctorId, null, null, null, null);
        Result<FinancialData> financialResult = await LoadFinancialAsync(
            financialFilter, cancellationToken);
        if (financialResult.IsFailure)
        {
            return Result.Failure<ComparisonReportModel>(financialResult.Error);
        }

        FinancialData financial = financialResult.Value;
        (DateTimeOffset start, DateTimeOffset end) = Period(filter.From, filter.To);
        ComparisonAppointmentFact[] appointments = await dbContext.Appointments
            .AsNoTracking().Where(item => item.StartAt >= start && item.StartAt < end &&
                (!filter.DepartmentId.HasValue || item.DepartmentId == filter.DepartmentId) &&
                (!filter.DoctorId.HasValue || item.Services.Any(line =>
                    line.DoctorService.DoctorId == filter.DoctorId &&
                    line.Status != AppointmentServiceStatus.Superseded)))
            .Select(item => new ComparisonAppointmentFact(item.StartAt,
                item.DepartmentId, item.Services.Where(line =>
                        line.Status != AppointmentServiceStatus.Superseded)
                    .Select(line => line.Service.Department.Name).First()))
            .Take(MaximumFactRows + 1).ToArrayAsync(cancellationToken);
        if (TooLarge(appointments))
        {
            return Result.Failure<ComparisonReportModel>(
                ReportingErrors.ResultTooLarge);
        }

        ComparisonDoctorFact[] doctorAppointments = filter.GroupBy != ReportGroupBy.Doctor
            ? [] : await dbContext.AppointmentServices.AsNoTracking().Where(line =>
                    line.Appointment.StartAt >= start && line.Appointment.StartAt < end &&
                    line.Status != AppointmentServiceStatus.Superseded &&
                    (!filter.DepartmentId.HasValue ||
                        line.DepartmentId == filter.DepartmentId) &&
                    (!filter.DoctorId.HasValue ||
                        line.DoctorService.DoctorId == filter.DoctorId))
                .Select(line => new ComparisonDoctorFact(line.AppointmentId,
                    line.DoctorService.DoctorId, line.DoctorService.Doctor.Name))
                .Distinct().Take(MaximumFactRows + 1)
                .ToArrayAsync(cancellationToken);
        if (TooLarge(doctorAppointments))
        {
            return Result.Failure<ComparisonReportModel>(
                ReportingErrors.ResultTooLarge);
        }

        ComparisonPointModel[] points = filter.GroupBy switch
        {
            ReportGroupBy.Day or ReportGroupBy.Month => TimeComparison(filter,
                financial, appointments),
            ReportGroupBy.Department => DimensionComparison(financial, appointments),
            ReportGroupBy.Doctor => DoctorComparison(financial, doctorAppointments),
            _ => []
        };
        return Result.Success(new ComparisonReportModel(filter.From, filter.To,
            filter.GroupBy, points));
    }

    public async Task<Result<ShiftReportPage>> GetShiftsAsync(ShiftReportFilter filter,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = Period(filter.From, filter.To);
        DateTimeOffset now = timeProvider.GetUtcNow();
        ShiftCandidate[] candidates = await (from shift in dbContext.Shifts.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on shift.CashDrawer.SecretaryUserId equals user.Id
            where shift.ScheduledStart >= start && shift.ScheduledStart < end &&
                (!filter.SecretaryUserId.HasValue ||
                    user.Id == filter.SecretaryUserId.Value)
            orderby shift.ScheduledStart descending, shift.Id descending
            select new ShiftCandidate(shift.Id, user.Id, user.FullName,
                shift.ScheduledStart, shift.ScheduledEnd, shift.GraceEndsAt,
                shift.Status, shift.OpeningBalance, shift.DeclaredCash,
                shift.CashVariance)).Take(MaximumFactRows + 1)
            .ToArrayAsync(cancellationToken);
        if (TooLarge(candidates))
        {
            return Result.Failure<ShiftReportPage>(ReportingErrors.ResultTooLarge);
        }

        ShiftCandidate[] filtered = candidates.Where(item => !filter.Status.HasValue ||
            EffectiveStatus(item, now) == filter.Status.Value).ToArray();
        ShiftCandidate[] page = filtered.Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize).ToArray();
        long[] shiftIds = page.Select(item => item.Id).ToArray();
        ShiftMoney[] collectionRows = await dbContext.PaymentMethodAllocations
            .AsNoTracking().Where(item => shiftIds.Contains(item.Payment.ShiftId))
            .Select(item => new ShiftMoney(item.Payment.ShiftId, item.Amount,
                item.PaymentMethod.IsCash)).ToArrayAsync(cancellationToken);
        ShiftMoney[] refundRows = await dbContext.RefundMethodAllocations.AsNoTracking()
            .Where(item => shiftIds.Contains(item.Refund.ExecutionShiftId) &&
                item.Refund.Status == RefundStatus.Posted)
            .Select(item => new ShiftMoney(item.Refund.ExecutionShiftId, item.Amount,
                item.OriginalAllocation.PaymentMethod.IsCash))
            .ToArrayAsync(cancellationToken);
        Dictionary<long, decimal> withdrawals = await dbContext.CashWithdrawals
            .AsNoTracking().Where(item => shiftIds.Contains(item.ShiftId) &&
                item.Status == CashWithdrawalStatus.Executed)
            .GroupBy(item => item.ShiftId).ToDictionaryAsync(group => group.Key,
                group => group.Sum(item => item.Amount), cancellationToken);
        ShiftReportItemModel[] items = page.Select(item =>
        {
            ShiftMoney[] collected = collectionRows.Where(row => row.ShiftId == item.Id)
                .ToArray();
            ShiftMoney[] refunded = refundRows.Where(row => row.ShiftId == item.Id)
                .ToArray();
            decimal cash = collected.Where(row => row.IsCash).Sum(row => row.Amount);
            decimal cashRefunded = refunded.Where(row => row.IsCash).Sum(row => row.Amount);
            decimal withdrawn = withdrawals.GetValueOrDefault(item.Id);
            decimal? expected = item.OpeningBalance.HasValue
                ? item.OpeningBalance + cash - cashRefunded - withdrawn : null;
            return new ShiftReportItemModel(item.Id, item.SecretaryId,
                item.SecretaryName, item.ScheduledStart, item.ScheduledEnd,
                EffectiveStatus(item, now), item.OpeningBalance, cash,
                collected.Sum(row => row.Amount) - cash, cashRefunded,
                refunded.Sum(row => row.Amount) - cashRefunded, withdrawn, expected,
                item.DeclaredCash, item.Variance);
        }).ToArray();
        return Result.Success(new ShiftReportPage(items, filter.PageNumber,
            filter.PageSize, filtered.Length));
    }

    public async Task<Result<AuditLogPage>> GetAuditLogsAsync(AuditLogFilter filter,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = Period(filter.From, filter.To);
        IQueryable<AuditLog> query = dbContext.AuditLogs.AsNoTracking()
            .Where(item => item.OccurredAt >= start && item.OccurredAt < end &&
                (!filter.ActorUserId.HasValue || item.ActorUserId == filter.ActorUserId) &&
                (!filter.ActorType.HasValue || item.ActorType == filter.ActorType));
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            string action = filter.Action.Trim();
            query = query.Where(item => item.Action.Contains(action));
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            string entityType = filter.EntityType.Trim();
            query = query.Where(item => item.EntityType.Contains(entityType));
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            string entityId = filter.EntityId.Trim();
            query = query.Where(item => item.EntityId == entityId);
        }

        int total = await query.CountAsync(cancellationToken);
        AuditProjection[] rows = await (from audit in query
            join user in dbContext.Users.AsNoTracking() on audit.ActorUserId equals user.Id
                into users
            from user in users.DefaultIfEmpty()
            orderby audit.OccurredAt descending, audit.Id descending
            select new AuditProjection(audit.Id, audit.ActorUserId,
                user == null ? null : user.FullName, audit.ActorType, audit.Action,
                audit.EntityType, audit.EntityId, audit.OccurredAt, audit.Reason))
            .Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new AuditLogPage(rows.Select(item => new AuditLogModel(
            item.Id, item.ActorUserId, item.ActorName, item.ActorType, item.Action,
            item.EntityType, item.EntityId, item.OccurredAt, item.Reason)).ToArray(),
            filter.PageNumber, filter.PageSize, total));
    }

    private async Task<Result<FinancialData>> LoadFinancialAsync(
        FinancialReportFilter filter,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = Period(filter.From, filter.To);
        FinancialFact[] appointments = await (from allocation in dbContext
                .AppointmentPaymentAllocations.AsNoTracking()
            from line in dbContext.AppointmentServices.AsNoTracking().Where(item =>
                item.AppointmentId == allocation.AppointmentId &&
                item.Status != AppointmentServiceStatus.Superseded)
            where allocation.Payment.CollectedAt >= start &&
                allocation.Payment.CollectedAt < end &&
                (!filter.DepartmentId.HasValue ||
                    line.DepartmentId == filter.DepartmentId.Value) &&
                (!filter.ServiceId.HasValue || line.ServiceId == filter.ServiceId.Value) &&
                (!filter.DoctorId.HasValue ||
                    line.DoctorService.DoctorId == filter.DoctorId.Value) &&
                (!filter.SecretaryUserId.HasValue || allocation.Payment.CollectedByUserId ==
                    filter.SecretaryUserId.Value) &&
                (!filter.ShiftId.HasValue || allocation.Payment.ShiftId == filter.ShiftId) &&
                (!filter.PatientId.HasValue || allocation.PatientId == filter.PatientId)
            select new FinancialFact(allocation.PaymentId,
                allocation.Payment.CollectedAt, line.DepartmentId,
                line.Service.Department.Name, line.ServiceId, line.Service.Name,
                line.DoctorService.DoctorId, line.DoctorService.Doctor.Name,
                line.NetAmount, line.DiscountAmount, false))
            .Take(MaximumFactRows + 1).ToArrayAsync(cancellationToken);
        if (TooLarge(appointments))
        {
            return Result.Failure<FinancialData>(ReportingErrors.ResultTooLarge);
        }

        FinancialFact[] packages = filter.ServiceId.HasValue || filter.DoctorId.HasValue
            ? [] : await dbContext.PackagePaymentAllocations.AsNoTracking()
                .Where(item => item.Payment.CollectedAt >= start &&
                    item.Payment.CollectedAt < end &&
                    (!filter.DepartmentId.HasValue ||
                        item.PatientPackage.DepartmentId == filter.DepartmentId) &&
                    (!filter.SecretaryUserId.HasValue || item.Payment.CollectedByUserId ==
                        filter.SecretaryUserId) &&
                    (!filter.ShiftId.HasValue || item.Payment.ShiftId == filter.ShiftId) &&
                    (!filter.PatientId.HasValue || item.PatientId == filter.PatientId))
                .Select(item => new FinancialFact(item.PaymentId,
                    item.Payment.CollectedAt, item.PatientPackage.DepartmentId,
                    item.PatientPackage.DepartmentNameSnapshot, null,
                    item.PatientPackage.PackageNameSnapshot, null, null, item.Amount,
                    item.PatientPackage.DiscountAmountSnapshot, true))
                .Take(MaximumFactRows + 1).ToArrayAsync(cancellationToken);
        if (TooLarge(packages) || appointments.Length + packages.Length > MaximumFactRows)
        {
            return Result.Failure<FinancialData>(ReportingErrors.ResultTooLarge);
        }

        FinancialFact[] collections = appointments.Concat(packages).ToArray();
        HashSet<long> paymentIds = collections.Select(item => item.MovementId).ToHashSet();
        MethodFact[] collectionMethodCandidates = paymentIds.Count == 0 ? [] :
            await dbContext.PaymentMethodAllocations.AsNoTracking()
            .Where(item => item.Payment.CollectedAt >= start &&
                item.Payment.CollectedAt < end &&
                (!filter.SecretaryUserId.HasValue ||
                    item.Payment.CollectedByUserId == filter.SecretaryUserId) &&
                (!filter.ShiftId.HasValue || item.Payment.ShiftId == filter.ShiftId))
            .Select(item => new MethodFact(item.PaymentId, item.PaymentMethodId,
                item.PaymentMethod.DisplayName, item.PaymentMethod.IsCash, item.Amount,
                item.Payment.TotalAmount)).Take(MaximumFactRows + 1)
            .ToArrayAsync(cancellationToken);
        if (TooLarge(collectionMethodCandidates))
        {
            return Result.Failure<FinancialData>(ReportingErrors.ResultTooLarge);
        }

        MethodFact[] collectionMethods = collectionMethodCandidates.Where(item =>
            paymentIds.Contains(item.MovementId)).ToArray();

        RefundFact[] refunds = await (from allocation in dbContext
                .RefundAppointmentAllocations.AsNoTracking()
            from line in dbContext.AppointmentServices.AsNoTracking().Where(item =>
                item.AppointmentId == allocation.OriginalAllocation.AppointmentId &&
                item.Status != AppointmentServiceStatus.Superseded)
            where allocation.Refund.ExecutedAt >= start &&
                allocation.Refund.ExecutedAt < end &&
                allocation.Refund.Status == RefundStatus.Posted &&
                (!filter.DepartmentId.HasValue || line.DepartmentId == filter.DepartmentId) &&
                (!filter.ServiceId.HasValue || line.ServiceId == filter.ServiceId) &&
                (!filter.DoctorId.HasValue ||
                    line.DoctorService.DoctorId == filter.DoctorId) &&
                (!filter.SecretaryUserId.HasValue || allocation.Refund.ExecutedByUserId ==
                    filter.SecretaryUserId) &&
                (!filter.ShiftId.HasValue || allocation.Refund.ExecutionShiftId ==
                    filter.ShiftId) &&
                (!filter.PatientId.HasValue ||
                    allocation.OriginalAllocation.PatientId == filter.PatientId)
            select new RefundFact(allocation.RefundId, allocation.Refund.ExecutedAt,
                line.DepartmentId, line.Service.Department.Name, line.ServiceId,
                line.Service.Name, line.DoctorService.DoctorId,
                line.DoctorService.Doctor.Name,
                allocation.Amount * line.NetAmount /
                    allocation.OriginalAllocation.Amount))
            .Take(MaximumFactRows + 1).ToArrayAsync(cancellationToken);
        if (TooLarge(refunds))
        {
            return Result.Failure<FinancialData>(ReportingErrors.ResultTooLarge);
        }

        HashSet<long> refundIds = refunds.Select(item => item.MovementId).ToHashSet();
        RefundMethodFact[] refundMethodCandidates = refundIds.Count == 0 ? [] :
            await dbContext.RefundMethodAllocations.AsNoTracking()
            .Where(item => item.Refund.ExecutedAt >= start &&
                item.Refund.ExecutedAt < end &&
                item.Refund.Status == RefundStatus.Posted &&
                (!filter.SecretaryUserId.HasValue ||
                    item.Refund.ExecutedByUserId == filter.SecretaryUserId) &&
                (!filter.ShiftId.HasValue ||
                    item.Refund.ExecutionShiftId == filter.ShiftId))
            .Select(item => new RefundMethodFact(item.RefundId,
                item.OriginalAllocation.PaymentMethodId,
                item.OriginalAllocation.PaymentMethod.DisplayName,
                item.OriginalAllocation.PaymentMethod.IsCash, item.Amount,
                item.Refund.Amount)).Take(MaximumFactRows + 1)
            .ToArrayAsync(cancellationToken);
        if (TooLarge(refundMethodCandidates))
        {
            return Result.Failure<FinancialData>(ReportingErrors.ResultTooLarge);
        }

        RefundMethodFact[] refundMethods = refundMethodCandidates.Where(item =>
            refundIds.Contains(item.MovementId)).ToArray();
        return Result.Success(new FinancialData(start, end, collections, refunds,
            collectionMethods, refundMethods, collectionMethods.Where(item => item.IsCash)
                .Select(item => item.MethodId).ToHashSet()));
    }

    private static FinancialBreakdownModel[] MethodBreakdown(FinancialData data,
        long? paymentMethodId, decimal collected, decimal refunded)
    {
        Dictionary<long, decimal> collectionTarget = data.Collections
            .GroupBy(item => item.MovementId).ToDictionary(group => group.Key,
                group => group.Sum(item => item.Amount));
        Dictionary<long, decimal> refundTarget = data.Refunds
            .GroupBy(item => item.MovementId).ToDictionary(group => group.Key,
                group => group.Sum(item => item.Amount));
        return BuildBreakdown(data.CollectionMethods.Where(item =>
                    !paymentMethodId.HasValue || item.MethodId == paymentMethodId.Value)
                .Select(item => new BreakdownShare(item.MethodId, item.MethodName,
                    item.Amount * collectionTarget.GetValueOrDefault(item.MovementId) /
                    item.MovementTotal)),
            data.RefundMethods.Where(item => !paymentMethodId.HasValue ||
                    item.MethodId == paymentMethodId.Value)
                .Select(item => new BreakdownShare(item.MethodId, item.MethodName,
                    item.Amount * refundTarget.GetValueOrDefault(item.MovementId) /
                    item.MovementTotal)), collected, refunded, orderByNet: false);
    }

    private static FinancialBreakdownModel[] BuildBreakdown(
        IEnumerable<BreakdownShare> collectionShares,
        IEnumerable<BreakdownShare> refundShares, decimal collected, decimal refunded,
        bool orderByNet)
    {
        Dictionary<BreakdownKey, decimal> collectionAmounts = AllocateRounded(
            collectionShares, collected);
        Dictionary<BreakdownKey, decimal> refundAmounts = AllocateRounded(
            refundShares, refunded);
        IEnumerable<FinancialBreakdownModel> result = collectionAmounts.Keys
            .Union(refundAmounts.Keys).Select(key =>
            {
                decimal collectedAmount = collectionAmounts.GetValueOrDefault(key);
                decimal refundedAmount = refundAmounts.GetValueOrDefault(key);
                return new FinancialBreakdownModel(key.Id, key.Name, collectedAmount,
                    refundedAmount, collectedAmount - refundedAmount);
            });
        return orderByNet
            ? result.OrderByDescending(item => item.Net).ThenBy(item => item.Name)
                .ToArray()
            : result.OrderBy(item => item.Name).ToArray();
    }

    private static Dictionary<BreakdownKey, decimal> AllocateRounded(
        IEnumerable<BreakdownShare> shares, decimal expectedTotal)
    {
        AllocatedShare[] allocated = shares.GroupBy(item =>
                new BreakdownKey(item.Id, item.Name))
            .Select(group =>
            {
                decimal exactCents = group.Sum(item => item.Amount) * 100m;
                long wholeCents = decimal.ToInt64(decimal.Floor(exactCents));
                return new AllocatedShare(group.Key, wholeCents,
                    exactCents - wholeCents);
            }).OrderByDescending(item => item.Fraction)
            .ThenBy(item => item.Key.Id).ThenBy(item => item.Key.Name).ToArray();
        long remainingCents = decimal.ToInt64(expectedTotal * 100m) -
            allocated.Sum(item => item.Cents);
        for (int index = 0; index < remainingCents; index++)
        {
            allocated[index % allocated.Length].Cents++;
        }

        return allocated.ToDictionary(item => item.Key,
            item => item.Cents / 100m);
    }

    private static Dictionary<long, decimal> MethodFactors(
        IEnumerable<MethodFact> methods, long? methodId) => methods
        .GroupBy(item => item.MovementId).ToDictionary(group => group.Key,
            group => methodId.HasValue
                ? group.Where(item => item.MethodId == methodId.Value)
                    .Sum(item => item.Amount) / group.First().MovementTotal : 1m);

    private static Dictionary<long, decimal> RefundMethodFactors(
        IEnumerable<RefundMethodFact> methods, long? methodId) => methods
        .GroupBy(item => item.MovementId).ToDictionary(group => group.Key,
            group => methodId.HasValue
                ? group.Where(item => item.MethodId == methodId.Value)
                    .Sum(item => item.Amount) / group.First().MovementTotal : 1m);

    private static ComparisonPointModel[] TimeComparison(ComparisonReportFilter filter,
        FinancialData financial, IReadOnlyCollection<ComparisonAppointmentFact> appointments)
    {
        string Key(DateTimeOffset instant) => filter.GroupBy == ReportGroupBy.Day
            ? CashierInfrastructureSupport.ClinicDate(instant)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : CashierInfrastructureSupport.ClinicDate(instant)
                .ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var movements = financial.Collections.Select(item => new
            { Key = Key(item.OccurredAt), Collected = item.Amount, Refunded = 0m })
            .Concat(financial.Refunds.Select(item => new
                { Key = Key(item.OccurredAt), Collected = 0m, Refunded = item.Amount }));
        Dictionary<string, int> counts = appointments.GroupBy(item => Key(item.StartAt))
            .ToDictionary(group => group.Key, group => group.Count());
        return movements.GroupBy(item => item.Key).Select(group =>
        {
            decimal collected = Money(group.Sum(item => item.Collected));
            decimal refunded = Money(group.Sum(item => item.Refunded));
            return new ComparisonPointModel(group.Key, group.Key, collected, refunded,
                collected - refunded, counts.GetValueOrDefault(group.Key));
        }).Concat(counts.Where(item => !movements.Any(row => row.Key == item.Key))
            .Select(item => new ComparisonPointModel(item.Key, item.Key, 0, 0, 0,
                item.Value))).OrderBy(item => item.Key).ToArray();
    }

    private static ComparisonPointModel[] DimensionComparison(FinancialData financial,
        IReadOnlyCollection<ComparisonAppointmentFact> appointments)
    {
        var money = financial.Collections.GroupBy(item => new
            { item.DimensionId, item.DimensionName }).Select(group => new
            {
                group.Key.DimensionId, group.Key.DimensionName,
                Collected = group.Sum(item => item.Amount), Refunded = 0m
            }).Concat(financial.Refunds.GroupBy(item => new
                { item.DimensionId, item.DimensionName }).Select(group => new
                {
                    group.Key.DimensionId, group.Key.DimensionName,
                    Collected = 0m, Refunded = group.Sum(item => item.Amount)
                }));
        Dictionary<long, DepartmentCount> counts = appointments.GroupBy(item =>
                new { item.DepartmentId, item.DepartmentName })
            .ToDictionary(group => group.Key.DepartmentId, group =>
                new DepartmentCount(group.Key.DepartmentName, group.Count()));
        ComparisonPointModel[] monetaryPoints = money.GroupBy(item =>
                new { item.DimensionId, item.DimensionName })
            .Select(group =>
            {
                decimal collected = Money(group.Sum(item => item.Collected));
                decimal refunded = Money(group.Sum(item => item.Refunded));
                return new ComparisonPointModel(group.Key.DimensionId.ToString(
                        CultureInfo.InvariantCulture),
                    group.Key.DimensionName, collected, refunded,
                    collected - refunded,
                    counts.GetValueOrDefault(group.Key.DimensionId)?.Count ?? 0);
            }).ToArray();
        return monetaryPoints.Concat(counts.Where(item => !monetaryPoints.Any(point =>
                point.Key == item.Key.ToString(CultureInfo.InvariantCulture)))
            .Select(item => new ComparisonPointModel(item.Key.ToString(
                    CultureInfo.InvariantCulture), item.Value.Name,
                0, 0, 0, item.Value.Count)))
            .OrderByDescending(item => item.Net).ThenBy(item => item.Label).ToArray();
    }

    private static ComparisonPointModel[] DoctorComparison(FinancialData financial,
        IReadOnlyCollection<ComparisonDoctorFact> appointments)
    {
        var movements = financial.Collections.Where(item => item.DoctorId.HasValue)
            .Select(item => new { Id = item.DoctorId!.Value,
                Name = item.DoctorName!, Collected = item.Amount, Refunded = 0m })
            .Concat(financial.Refunds.Where(item => item.DoctorId.HasValue)
                .Select(item => new { Id = item.DoctorId!.Value,
                    Name = item.DoctorName!, Collected = 0m, Refunded = item.Amount }));
        Dictionary<long, int> appointmentCounts = appointments.GroupBy(item =>
                item.DoctorId).ToDictionary(group => group.Key,
                group => group.Select(item => item.AppointmentId).Distinct().Count());
        return movements.GroupBy(item => new { item.Id, item.Name }).Select(group =>
        {
            decimal collected = Money(group.Sum(item => item.Collected));
            decimal refunded = Money(group.Sum(item => item.Refunded));
            return new ComparisonPointModel(group.Key.Id.ToString(
                    CultureInfo.InvariantCulture), group.Key.Name,
                collected, refunded, collected - refunded,
                appointmentCounts.GetValueOrDefault(group.Key.Id));
        }).Concat(appointmentCounts.Where(item => !movements.Any(row =>
                row.Id == item.Key)).Select(item =>
            {
                ComparisonDoctorFact doctor = appointments.First(row =>
                    row.DoctorId == item.Key);
                return new ComparisonPointModel(item.Key.ToString(
                        CultureInfo.InvariantCulture), doctor.DoctorName,
                    0, 0, 0, item.Value);
            })).OrderByDescending(item => item.Net).ThenBy(item => item.Label).ToArray();
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Period(DateOnly from,
        DateOnly to) => (CashierInfrastructureSupport.ClinicDayStartUtc(from),
            CashierInfrastructureSupport.ClinicDayStartUtc(to.AddDays(1)));

    private static bool HasTargetDimension(FinancialReportFilter filter) =>
        filter.DepartmentId.HasValue || filter.ServiceId.HasValue ||
        filter.DoctorId.HasValue || filter.PaymentMethodId.HasValue ||
        filter.PatientId.HasValue;

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool TooLarge<T>(IReadOnlyCollection<T> rows) =>
        rows.Count > MaximumFactRows;

    private static int Count(IEnumerable<DashboardAppointmentFact> items,
        AppointmentStatus status) => items.Count(item => item.Status == status);
    private static int Count(IEnumerable<OperationalAppointmentFact> items,
        AppointmentStatus status) => items.Count(item => item.Status == status);

    private static bool MatchesAge(OperationalAppointmentFact item, int? minimum,
        int? maximum, DateOnly onDate)
    {
        int? age = item.BirthDate.HasValue
            ? Age(item.BirthDate.Value, onDate)
            : item.AgeAtRegistration.HasValue && item.AgeRecordedAt.HasValue
                ? item.AgeAtRegistration + Age(item.AgeRecordedAt.Value, onDate) : null;
        return (!minimum.HasValue || age >= minimum) &&
            (!maximum.HasValue || age <= maximum);
    }

    private static int Age(DateOnly start, DateOnly end)
    {
        int years = end.Year - start.Year;
        return start.AddYears(years) > end ? years - 1 : years;
    }

    private static ShiftStatus EffectiveStatus(ShiftCandidate shift,
        DateTimeOffset now) => shift.Status is ShiftStatus.Closed or ShiftStatus.Cancelled
            ? shift.Status : now < shift.ScheduledStart ? ShiftStatus.Scheduled
            : now < shift.ScheduledEnd ? ShiftStatus.Open : ShiftStatus.Grace;

    private sealed record DashboardMethodFact(decimal Amount, bool IsCash);
    private sealed record DashboardAppointmentFact(AppointmentStatus Status,
        PaymentStatus PaymentStatus, decimal NetAmount, long PatientId,
        DateTimeOffset PatientCreatedAt);
    private sealed record FinancialFact(long MovementId, DateTimeOffset OccurredAt,
        long DimensionId, string DimensionName, long? ServiceId, string ServiceName,
        long? DoctorId, string? DoctorName, decimal Amount, decimal DiscountAmount,
        bool IsPackage);
    private sealed record RefundFact(long MovementId, DateTimeOffset OccurredAt,
        long DimensionId, string DimensionName, long ServiceId, string ServiceName,
        long? DoctorId, string? DoctorName, decimal Amount);
    private sealed record MethodFact(long MovementId, long MethodId, string MethodName,
        bool IsCash, decimal Amount, decimal MovementTotal);
    private sealed record RefundMethodFact(long MovementId, long MethodId,
        string MethodName, bool IsCash, decimal Amount, decimal MovementTotal);
    private sealed record FinancialData(DateTimeOffset Start, DateTimeOffset End,
        FinancialFact[] Collections, RefundFact[] Refunds,
        MethodFact[] CollectionMethods, RefundMethodFact[] RefundMethods,
        HashSet<long> CashMethodIds);
    private sealed record BreakdownShare(long? Id, string Name, decimal Amount);
    private sealed record BreakdownKey(long? Id, string Name);
    private sealed class AllocatedShare(BreakdownKey key, long cents,
        decimal fraction)
    {
        public BreakdownKey Key { get; } = key;
        public long Cents { get; set; } = cents;
        public decimal Fraction { get; } = fraction;
    }

    private sealed record OperationalAppointmentFact(long Id, long PatientId,
        long DepartmentId, string DepartmentName, AppointmentStatus Status,
        DateTimeOffset PatientCreatedAt, DateOnly? BirthDate, int? AgeAtRegistration,
        DateOnly? AgeRecordedAt);
    private sealed record OperationalServiceFact(long AppointmentId, long DepartmentId,
        string DepartmentName, AppointmentStatus AppointmentStatus, long LineId,
        int Quantity, decimal NetAmount, decimal PackageCoveredAmount, long DoctorId,
        string DoctorName, int DeviceCount);
    private sealed record ComparisonAppointmentFact(DateTimeOffset StartAt,
        long DepartmentId, string DepartmentName);
    private sealed record DepartmentCount(string Name, int Count);
    private sealed record ComparisonDoctorFact(long AppointmentId, long DoctorId,
        string DoctorName);
    private sealed record ShiftCandidate(long Id, long SecretaryId, string SecretaryName,
        DateTimeOffset ScheduledStart, DateTimeOffset ScheduledEnd,
        DateTimeOffset GraceEndsAt, ShiftStatus Status, decimal? OpeningBalance,
        decimal? DeclaredCash, decimal? Variance);
    private sealed record ShiftMoney(long ShiftId, decimal Amount, bool IsCash);
    private sealed record AuditProjection(long Id, long? ActorUserId, string? ActorName,
        AuditActorType ActorType, string Action, string EntityType,
        string EntityId, DateTimeOffset OccurredAt, string? Reason);
}
