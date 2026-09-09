using System.Globalization;
using System.Text.Json;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Domain.Auditing;
using Clinic.Domain.Cashier;
using Clinic.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Cashier;

internal static class CashierInfrastructureSupport
{
    public static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        DateTime local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        if (ClinicTimeZone.IsInvalidTime(local))
        {
            throw new ArgumentException("الوقت المحدد غير صالح حسب توقيت العيادة.");
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, ClinicTimeZone));
    }

    public static DateOnly ClinicDate(DateTimeOffset value) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(value, ClinicTimeZone).DateTime);

    public static DateTimeOffset ClinicDayStartUtc(DateOnly date) => ToUtc(date, TimeOnly.MinValue);

    public static bool MatchesVersion(byte[] current, byte[] supplied) =>
        current.AsSpan().SequenceEqual(supplied);

    public static ShiftModel Map(Shift shift, long secretaryUserId,
        string secretaryName, DateTimeOffset now, bool adminCapabilities)
    {
        ShiftStatus effectiveStatus = shift.GetEffectiveStatus(now);
        bool active = effectiveStatus is ShiftStatus.Open or ShiftStatus.Grace;
        return new ShiftModel(
            shift.Id,
            shift.CashDrawerId,
            secretaryUserId,
            secretaryName,
            shift.ScheduledStart,
            shift.ScheduledEnd,
            shift.GraceEndsAt,
            shift.ActualOpenedAt,
            shift.ClosedAt,
            shift.OpeningBalance,
            shift.ExpectedCash,
            shift.DeclaredCash,
            shift.CashVariance,
            effectiveStatus,
            effectiveStatus == ShiftStatus.Grace && now >= shift.GraceEndsAt,
            active && !shift.OpeningBalance.HasValue,
            shift.CanCollect(now),
            active && shift.OpeningBalance.HasValue &&
                (adminCapabilities || now >= shift.ScheduledEnd),
            active && shift.DeclaredCash.HasValue &&
                (adminCapabilities || now >= shift.ScheduledEnd),
            shift.CancelledAt,
            shift.CancellationReason,
            Convert.ToBase64String(shift.RowVersion));
    }

    public static AuditLog Audit(long actorUserId, string action, string entityType,
        long entityId, DateTimeOffset occurredAt, string? reason = null,
        object? data = null) => AuditLog.CreateForUser(
            actorUserId,
            action,
            entityType,
            entityId.ToString(CultureInfo.InvariantCulture),
            occurredAt,
            reason,
            data is null ? null : JsonSerializer.Serialize(data));

    public static bool IsUniqueViolation(DbUpdateException exception,
        string indexName) => exception.InnerException is SqlException
        {
            Number: 2601 or 2627
        } sqlException && sqlException.Message.Contains(indexName,
            StringComparison.Ordinal);

    private static TimeZoneInfo ResolveClinicTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
    }
}
