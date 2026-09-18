using AppointmentLine = Clinic.Domain.Appointments.AppointmentService;

namespace Clinic.Infrastructure.Appointments;

internal static class PackageSessionLifecycle
{
    public static async Task<PatientPackage> LoadUsableAsync(ClinicDbContext dbContext,
        Appointment appointment, long patientPackageId, CancellationToken cancellationToken,
        IReadOnlyCollection<long>? requestedServiceIds = null)
    {
        PatientPackage? patientPackage = await dbContext.PatientPackages
            .Include(item => item.Package).ThenInclude(item => item.Department)
            .Include(item => item.Services).ThenInclude(item => item.SourcePackageService)
                .ThenInclude(item => item.Service)
            .Include(item => item.Services).ThenInclude(item => item.Sessions)
            .AsSplitQuery().SingleOrDefaultAsync(item => item.Id == patientPackageId,
                cancellationToken);
        if (patientPackage is null || patientPackage.PatientId != appointment.PatientId ||
            patientPackage.DepartmentId != appointment.DepartmentId ||
            patientPackage.Status != PatientPackageStatus.Active ||
            patientPackage.PaymentStatus == PatientPackagePaymentStatus.Unpaid ||
            patientPackage.Package.Department.IsArchived)
        {
            throw new DomainException("الباقة غير متاحة لهذا الحجز.");
        }

        DateTimeOffset? deadline = patientPackage.FirstUsedAt.HasValue
            ? patientPackage.ExpiresAt
            : patientPackage.ActivationDeadlineAt;
        if (deadline is null || appointment.StartAt >= deadline.Value)
        {
            throw new DomainException("موعد الحجز خارج صلاحية الباقة.");
        }

        long[] serviceIds = requestedServiceIds?.ToArray() ?? appointment.Services
            .Where(item => item.Status == AppointmentServiceStatus.Scheduled)
            .Select(item => item.ServiceId).ToArray();
        if (serviceIds.Length == 0 || serviceIds.Length != serviceIds.Distinct().Count() ||
            patientPackage.Services.Count(item => serviceIds.Contains(item.ServiceId)) !=
            serviceIds.Length || patientPackage.Services.Where(item =>
                serviceIds.Contains(item.ServiceId)).Any(item =>
                    item.SourcePackageService.Service.IsArchived ||
                    !item.SourcePackageService.Service.IsActive))
        {
            throw new DomainException("خدمات الحجز لا تطابق خدمات الباقة المتاحة.");
        }

        return patientPackage;
    }

    public static IReadOnlyDictionary<long, decimal> Prices(PatientPackage patientPackage,
        IEnumerable<long> serviceIds) => patientPackage.Services
        .Where(item => serviceIds.Contains(item.ServiceId))
        .ToDictionary(item => item.ServiceId, item => item.UnitPriceSnapshot);

    public static async Task EnsureBalanceAsync(ClinicDbContext dbContext,
        PatientPackage patientPackage, IReadOnlyCollection<long> serviceIds,
        long? existingAppointmentId, CancellationToken cancellationToken)
    {
        long[] reusableSessionIds = existingAppointmentId.HasValue
            ? await dbContext.PackageSessionBookings.AsNoTracking()
                .Where(item => item.AppointmentId == existingAppointmentId.Value &&
                    item.Status == PackageSessionBookingStatus.Reserved)
                .Select(item => item.PackageSessionId).ToArrayAsync(cancellationToken)
            : [];
        if (patientPackage.Services.Where(item => serviceIds.Contains(item.ServiceId))
            .Any(item => !item.Sessions.Any(session =>
                session.Status == PackageSessionStatus.Available ||
                reusableSessionIds.Contains(session.Id))))
        {
            throw new DomainException("لا توجد جلسات متاحة لإحدى خدمات الباقة.");
        }
    }

    public static void Reserve(ClinicDbContext dbContext, Appointment appointment,
        PatientPackage patientPackage, long actorUserId, DateTimeOffset reservedAt)
    {
        foreach (AppointmentLine line in appointment.Services.Where(item =>
            item.Status == AppointmentServiceStatus.Scheduled))
        {
            PackageSession session = patientPackage.Services.Single(item =>
                    item.ServiceId == line.ServiceId).Sessions
                .Where(item => item.Status == PackageSessionStatus.Available)
                .OrderBy(item => item.SequenceNumber).FirstOrDefault()
                ?? throw new DomainException("لا توجد جلسات متاحة لإحدى خدمات الباقة.");
            PackageSessionBooking booking = session.Reserve(appointment, line, reservedAt);
            dbContext.PackageSessionBookings.Add(booking);
            AddAudit(dbContext, actorUserId, "packages.session_reserved", booking,
                reservedAt);
        }
    }

    public static async Task ReleaseAsync(ClinicDbContext dbContext, long appointmentId,
        long actorUserId, DateTimeOffset releasedAt, CancellationToken cancellationToken)
    {
        PackageSessionBooking[] bookings = await ReservedBookings(dbContext, appointmentId)
            .ToArrayAsync(cancellationToken);
        foreach (PackageSessionBooking booking in bookings)
        {
            booking.Release(releasedAt);
            AddAudit(dbContext, actorUserId, "packages.session_released", booking,
                releasedAt);
        }
    }

    public static async Task ReplaceAsync(ClinicDbContext dbContext, Appointment appointment,
        PatientPackage patientPackage, long actorUserId, DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        PackageSessionBooking[] previous = await ReservedBookings(dbContext, appointment.Id)
            .ToArrayAsync(cancellationToken);
        foreach (PackageSessionBooking booking in previous)
        {
            booking.Release(changedAt);
            AddAudit(dbContext, actorUserId, "packages.session_released", booking, changedAt);
        }

        foreach (AppointmentLine line in appointment.Services.Where(item =>
            item.Status == AppointmentServiceStatus.Scheduled))
        {
            PackageSession session = previous.Single(item => item.ServiceId == line.ServiceId)
                .Session;
            PackageSessionBooking replacement = session.Reserve(appointment, line, changedAt);
            dbContext.PackageSessionBookings.Add(replacement);
            AddAudit(dbContext, actorUserId, "packages.session_reserved", replacement,
                changedAt);
        }
    }

    public static async Task ConsumeAsync(ClinicDbContext dbContext, Appointment appointment,
        long actorUserId, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        if (recordedAt < appointment.EndAt || await dbContext.DepartmentClosures.AsNoTracking()
            .AnyAsync(item => item.DepartmentId == appointment.DepartmentId &&
                item.CancelledAt == null && item.StartAt <= recordedAt && recordedAt < item.EndAt,
                cancellationToken))
        {
            throw new DomainException("لا يمكن إكمال جلسة الباقة قبل انتهاء الموعد أو أثناء توقف القسم.");
        }

        PatientPackage patientPackage = await LoadUsableAsync(dbContext, appointment,
            appointment.PatientPackageId!.Value, cancellationToken);
        patientPackage.RecordFirstUse(appointment.StartAt);
        PackageSessionBooking[] bookings = await ReservedBookings(dbContext, appointment.Id)
            .ToArrayAsync(cancellationToken);
        if (bookings.Length != appointment.Services.Count(item =>
                item.Status == AppointmentServiceStatus.Scheduled))
        {
            throw new DomainException("جلسات الباقة المحجوزة لا تطابق خدمات الحجز.");
        }

        foreach (PackageSessionBooking booking in bookings)
        {
            booking.Consume(recordedAt);
            AddAudit(dbContext, actorUserId, "packages.session_consumed", booking, recordedAt);
        }

        if (patientPackage.ExpiresAt is DateTimeOffset expiresAt)
        {
            Appointment[] outsideValidity = await dbContext.Appointments
                .Where(item => item.Id != appointment.Id &&
                    item.PatientPackageId == patientPackage.Id && item.StartAt >= expiresAt &&
                    (item.Status == AppointmentStatus.Booked ||
                     item.Status == AppointmentStatus.Confirmed))
                .ToArrayAsync(cancellationToken);
            foreach (Appointment future in outsideValidity)
            {
                future.Suspend(actorUserId, recordedAt);
                AppointmentInfrastructureSupport.AddAudit(dbContext, actorUserId,
                    "appointments.suspended_package_expiry", future.Id, recordedAt);
            }
        }
    }

    private static IQueryable<PackageSessionBooking> ReservedBookings(
        ClinicDbContext dbContext, long appointmentId) => dbContext.PackageSessionBookings
        .Include(item => item.Session)
        .Where(item => item.AppointmentId == appointmentId &&
            item.Status == PackageSessionBookingStatus.Reserved);

    private static void AddAudit(ClinicDbContext dbContext, long actorUserId, string action,
        PackageSessionBooking booking, DateTimeOffset occurredAt) =>
        dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId, action,
            nameof(PackageSession), booking.PackageSessionId.ToString(
                CultureInfo.InvariantCulture),
            occurredAt, JsonSerializer.Serialize(new { booking.PatientPackageId,
                booking.PackageSessionId, booking.AppointmentId,
                booking.AppointmentServiceId })));
}
