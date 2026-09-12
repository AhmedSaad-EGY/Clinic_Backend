using System.Globalization;
using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Common;
using Clinic.Domain.Appointments;
using Clinic.Domain.Auditing;
using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Appointments;

internal static class AppointmentInfrastructureSupport
{
    public static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();

    public static DateTimeOffset ClinicDayStartUtc(DateOnly date)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, ClinicTimeZone), TimeSpan.Zero);
    }

    public static void AddAudit(ClinicDbContext dbContext, long actorUserId,
        string action, long appointmentId, DateTimeOffset occurredAt, string? reason = null) =>
        dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId, action,
            nameof(Appointment), appointmentId.ToString(CultureInfo.InvariantCulture),
            occurredAt, reason));

    public static void AddSystemAudit(ClinicDbContext dbContext, string action,
        long appointmentId, DateTimeOffset occurredAt) =>
        dbContext.AuditLogs.Add(AuditLog.CreateForSystem(action, nameof(Appointment),
            appointmentId.ToString(CultureInfo.InvariantCulture), occurredAt));

    public static bool MatchesVersion(byte[] actual, byte[] expected) =>
        actual.AsSpan().SequenceEqual(expected);

    public static ResultError MapDatabaseFailure(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
            ? AppointmentErrors.ConcurrencyConflict
            : throw new InvalidOperationException("Appointment persistence failed.", exception);

    public static AppointmentModel Map(Appointment appointment) => new(
        appointment.Id, appointment.PatientId,
        appointment.Patient.FileNumber.ToString("D6", CultureInfo.InvariantCulture),
        appointment.Patient.FullName, appointment.Patient.PrimaryPhoneNumber,
        appointment.DepartmentId, appointment.Room.Department.Name, appointment.RoomId,
        appointment.StartAt, appointment.EndAt, appointment.Status,
        appointment.PaymentStatus, appointment.SubtotalAmount,
        appointment.DiscountAmount, appointment.NetAmount, appointment.CreatedByUserId,
        appointment.CreatedAt, appointment.UpdatedByUserId, appointment.UpdatedAt,
        Convert.ToBase64String(appointment.RowVersion),
        appointment.Services.Where(item => item.Status != AppointmentServiceStatus.Superseded)
            .OrderBy(item => item.SequenceNumber)
            .Select(item => new AppointmentServiceModel(item.Id, item.ServiceId,
                item.Service.Name, item.DoctorService.DoctorId,
                item.DoctorService.Doctor.Name, item.SequenceNumber,
                item.SegmentStartAt, item.SegmentEndAt, item.Quantity, item.UnitPrice,
                item.NetAmount, item.Devices.Select(device => device.DeviceId).ToArray(),
                item.PackageCoveredAmount, item.PackageSessionBooking is null ? null :
                    new PackageSessionBookingModel(item.PackageSessionBooking.Id,
                        item.PackageSessionBooking.PackageSessionId,
                        item.PackageSessionBooking.Session.SequenceNumber,
                        item.PackageSessionBooking.Status,
                        item.PackageSessionBooking.ReservedAt,
                        item.PackageSessionBooking.ReleasedAt,
                        item.PackageSessionBooking.ConsumedAt), item.DiscountId,
                item.DiscountAmount, item.DiscountOverrideMode,
                item.DiscountOverrideByAdminUserId, item.DiscountOverrideReason))
            .ToArray(), appointment.PatientPackageId, appointment.PackageCoveredAmount);

    public static IQueryable<Appointment> Details(IQueryable<Appointment> query) => query
        .Include(item => item.Patient)
        .Include(item => item.Room).ThenInclude(room => room.Department)
        .Include(item => item.Services).ThenInclude(item => item.Service)
        .Include(item => item.Services).ThenInclude(item => item.DoctorService)
            .ThenInclude(item => item.Doctor)
        .Include(item => item.Services).ThenInclude(item => item.Devices)
        .Include(item => item.Services).ThenInclude(item => item.PackageSessionBooking)
            .ThenInclude(item => item!.Session)
        .AsSplitQuery();

    public static bool IsDoctorAvailable(DateTimeOffset from, DateTimeOffset to,
        IReadOnlyCollection<DoctorSchedule> schedules,
        IReadOnlyCollection<DoctorScheduleOverride> exceptions)
    {
        DateTimeOffset localFrom = TimeZoneInfo.ConvertTime(from, ClinicTimeZone);
        DateTimeOffset localTo = TimeZoneInfo.ConvertTime(to, ClinicTimeZone);
        DateOnly date = DateOnly.FromDateTime(localFrom.DateTime);
        if (date != DateOnly.FromDateTime(localTo.AddTicks(-1).DateTime)) return false;

        TimeOnly start = TimeOnly.FromDateTime(localFrom.DateTime);
        TimeOnly end = TimeOnly.FromDateTime(localTo.DateTime);
        ClinicDayOfWeek day = (ClinicDayOfWeek)(((int)localFrom.DayOfWeek + 6) % 7 + 1);
        if (exceptions.Any(item => item.ExceptionDate == date && item.CancelledAt == null &&
            item.Type == DoctorExceptionType.Unavailable &&
            (!item.StartTime.HasValue || item.StartTime < end && start < item.EndTime))) return false;
        if (exceptions.Any(item => item.ExceptionDate == date && item.CancelledAt == null &&
            item.Type == DoctorExceptionType.Available &&
            (!item.StartTime.HasValue || item.StartTime <= start && item.EndTime >= end))) return true;
        return schedules.Any(item => item.IsActive && item.DayOfWeek == day &&
            item.EffectiveFrom <= date && (!item.EffectiveTo.HasValue || item.EffectiveTo >= date) &&
            item.StartTime <= start && item.EndTime >= end);
    }

    private static TimeZoneInfo ResolveClinicTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
    }
}

internal static class AppointmentAuditActions
{
    public const string Created = "appointments.created";
    public const string Updated = "appointments.updated";
    public const string Cancelled = "appointments.cancelled";
    public const string Completed = "appointments.completed";
    public const string NoShow = "appointments.no_show";
    public const string Reactivated = "appointments.reactivated";
    public const string DoctorTransferred = "appointments.doctor_transferred";
}
