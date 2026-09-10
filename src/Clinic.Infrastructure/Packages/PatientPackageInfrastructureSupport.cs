using System.Globalization;
using System.Text.Json;
using Clinic.Application.Abstractions.Packages;
using Clinic.Domain.Auditing;
using Clinic.Domain.Packages;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Packages;

internal static class PatientPackageInfrastructureSupport
{
    public static PatientPackageModel Map(PatientPackage patientPackage,
        bool departmentStopped, DateTimeOffset now)
    {
        PackageSession[] sessions = patientPackage.Services.SelectMany(item => item.Sessions)
            .ToArray();
        bool serviceUnavailable = patientPackage.Services.Any(item =>
            item.SourcePackageService.Service.IsArchived ||
            !item.SourcePackageService.Service.IsActive);
        string? reason = patientPackage.Status != PatientPackageStatus.Active
            ? "الباقة ليست في حالة فعالة."
            : patientPackage.PaymentStatus == PatientPackagePaymentStatus.Unpaid
                ? "الباقة في انتظار السداد الكامل."
            : patientPackage.Package.Department.IsArchived
                ? "القسم مؤرشف."
            : departmentStopped ? "القسم متوقف مؤقتًا."
            : serviceUnavailable ? "إحدى خدمات الباقة غير متاحة حاليًا."
            : patientPackage.FirstUsedAt is null && patientPackage.ActivationDeadlineAt <= now
                ? "انتهت مهلة بدء الاستخدام."
            : patientPackage.ExpiresAt <= now ? "انتهت صلاحية الباقة."
            : null;

        return new PatientPackageModel(patientPackage.Id, patientPackage.PatientId,
            patientPackage.Patient.FileNumber, patientPackage.Patient.FullName,
            patientPackage.PackageId, patientPackage.DepartmentId,
            patientPackage.PackageNameSnapshot, patientPackage.DepartmentNameSnapshot,
            patientPackage.TotalSessions, patientPackage.BasePriceSnapshot,
            patientPackage.NetPriceSnapshot, patientPackage.ActivationGraceDaysSnapshot,
            patientPackage.UsageDurationDaysSnapshot, patientPackage.PaymentStatus,
            patientPackage.Status, reason is null, reason, patientPackage.RegisteredAt,
            patientPackage.ActivationWindowStartedAt, patientPackage.ActivationDeadlineAt,
            patientPackage.FirstUsedAt, patientPackage.ExpiresAt,
            sessions.Count(item => item.Status == PackageSessionStatus.Available),
            sessions.Count(item => item.Status == PackageSessionStatus.Reserved),
            sessions.Count(item => item.Status == PackageSessionStatus.Consumed),
            patientPackage.Services.OrderBy(item => item.ServiceNameSnapshot).Select(item =>
                new PatientPackageServiceModel(item.Id, item.ServiceId,
                    item.ServiceNameSnapshot, item.SpecializationIdSnapshot,
                    item.SpecializationNameSnapshot, item.SessionsPurchased,
                    item.UnitPriceSnapshot)).ToArray(),
            Convert.ToBase64String(patientPackage.RowVersion), DiscountId:
                patientPackage.DiscountId, DiscountAmount:
                patientPackage.DiscountAmountSnapshot);
    }

    public static PackageSessionModel Map(PackageSession session) => new(session.Id,
        session.PatientPackageServiceId, session.ServiceId,
        session.PatientPackageService.ServiceNameSnapshot, session.SequenceNumber,
        session.UnitPriceSnapshot, session.Status, session.ReservedAt, session.ConsumedAt,
        Convert.ToBase64String(session.RowVersion));

    public static Task<PatientPackagePaymentReferenceModel?> PaymentReferenceAsync(
        ClinicDbContext dbContext, long patientPackageId, CancellationToken cancellationToken) =>
        dbContext.PackagePaymentAllocations.AsNoTracking()
            .Where(item => item.PatientPackageId == patientPackageId)
            .Select(item => new PatientPackagePaymentReferenceModel(item.PaymentId,
                item.Payment.TransactionNumber, item.Payment.CollectedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public static void AddAudit(ClinicDbContext dbContext, long actorUserId, string action,
        long patientPackageId, DateTimeOffset occurredAt, object? details = null) =>
        dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId, action,
            nameof(PatientPackage), patientPackageId.ToString(CultureInfo.InvariantCulture),
            occurredAt, details is null ? null : JsonSerializer.Serialize(details)));
}

internal static class PatientPackageAuditActions
{
    public const string Registered = "packages.patient_package.registered";
    public const string Extended = "packages.patient_package.extended";
    public const string Paid = "packages.patient_package.paid";
}
