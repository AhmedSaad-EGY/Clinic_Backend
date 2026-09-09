using System.Globalization;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Domain.Auditing;
using Clinic.Domain.Packages;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Packages;

internal static class PackageInfrastructureSupport
{
    public static bool MatchesVersion(byte[] actual, byte[] expected) =>
        actual.AsSpan().SequenceEqual(expected);

    public static void AddAudit(ClinicDbContext dbContext, long actorUserId, string action,
        long packageId, DateTimeOffset occurredAt) => dbContext.AuditLogs.Add(
        AuditLog.CreateForUser(actorUserId, action, nameof(Package),
            packageId.ToString(CultureInfo.InvariantCulture), occurredAt));

    public static ResultError MapDatabaseFailure(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return PackageErrors.ConcurrencyConflict;
        }

        if (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
            sqlException.Message.Contains("UX_Packages_Name", StringComparison.Ordinal))
        {
            return PackageErrors.DuplicateName;
        }

        throw new InvalidOperationException("Package persistence failed.", exception);
    }

    public static PackageModel Map(Package package, bool departmentStopped)
    {
        PackageService[] activeServices = package.Services.Where(item => item.IsActive).ToArray();
        string? reason = package.IsArchived ? "الباقة مؤرشفة."
            : !package.IsActive ? "الباقة غير مفعلة."
            : package.ActivationGraceDays is null || package.UsageDurationDays is null
                ? "لم تُستكمل مهلة البداية ومدة الاستخدام."
            : departmentStopped ? "القسم متوقف مؤقتًا."
            : activeServices.Length == 0 ? "لا توجد خدمات فعالة داخل الباقة."
            : activeServices.Any(item => item.Service.IsArchived || !item.Service.IsActive)
                ? "إحدى خدمات الباقة غير متاحة."
            : activeServices.Any(item => item.Service.PricingMode != Domain.Catalog.PricingMode.Fixed)
                ? "إحدى خدمات الباقة لم تعد ذات سعر ثابت."
            : null;

        return new PackageModel(package.Id, package.DepartmentId, package.Department.Name,
            package.Name, package.SessionCount, package.BasePrice, package.ActivationGraceDays,
            package.UsageDurationDays, package.IsActive,
            package.IsArchived, reason is null, reason,
            activeServices.OrderBy(item => item.Service.Name).Select(item =>
                new PackageServiceModel(item.Id, item.ServiceId, item.Service.Name,
                    item.Service.SpecializationId, item.Service.Specialization.Name,
                    item.SessionsIncluded, item.UnitPriceAtDefinition, item.IsActive)).ToArray(),
            package.CreatedAt, package.UpdatedAt, Convert.ToBase64String(package.RowVersion));
    }
}

internal static class PackageAuditActions
{
    public const string Created = "packages.package.created";
    public const string Updated = "packages.package.updated";
    public const string ActivationChanged = "packages.package.activation_changed";
    public const string Archived = "packages.package.archived";
}
