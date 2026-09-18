namespace Clinic.Infrastructure.Appointments;

public sealed class AppointmentService : IAppointmentService
{
    private readonly ClinicDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly DiscountResolver _discountResolver;

    public AppointmentService(ClinicDbContext dbContext, TimeProvider timeProvider,
        DiscountResolver discountResolver)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _discountResolver = discountResolver;
    }

    public async Task<Result<AppointmentAvailability>> CheckAvailabilityAsync(
        long actorUserId, AppointmentInput input, long? excludedAppointmentId,
        CancellationToken cancellationToken)
    {
        Result<PreparedAppointment> prepared = await PrepareAsync(input, null,
            actorUserId, excludedAppointmentId, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<AppointmentAvailability>(prepared.Error);
        }

        if (!input.PatientPackageId.HasValue)
        {
            await _discountResolver.ApplyToAppointmentAsync(prepared.Value.Appointment,
                input.DiscountOverride, actorUserId, _timeProvider.GetUtcNow(),
                cancellationToken);
        }

        string? reason = await ConflictReasonAsync(prepared.Value.Appointment,
            excludedAppointmentId, cancellationToken);
        IReadOnlyCollection<AvailableSlot> alternatives = reason is null ||
            prepared.Value.Appointment.Status == AppointmentStatus.Suspended
            ? [] : await FindAlternativesAsync(prepared.Value, excludedAppointmentId,
                cancellationToken);
        return Result.Success(new AppointmentAvailability(reason is null,
            prepared.Value.Appointment.Status == AppointmentStatus.Suspended,
            prepared.Value.Appointment.EndAt, prepared.Value.Appointment.SubtotalAmount,
            reason, alternatives, prepared.Value.Appointment.DiscountAmount,
            prepared.Value.Appointment.NetAmount));
    }

    public async Task<Result<AppointmentModel>> CreateAsync(long actorUserId,
        AppointmentInput input, CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await _dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                string? fingerprint = null;
                if (input.PatientPackageId.HasValue)
                {
                    if (input.IdempotencyKey is not Guid key || key == Guid.Empty)
                    {
                        return Result.Failure<AppointmentModel>(AppointmentErrors.Validation(
                            "مفتاح منع تكرار الطلب مطلوب لحجز الباقة."));
                    }

                    fingerprint = Fingerprint(actorUserId, input);
                    await TransactionalResourceLock.AcquirePackageAppointmentRequestAsync(
                        _dbContext, key, cancellationToken);
                    Appointment? replay = await AppointmentInfrastructureSupport.Details(
                        _dbContext.Appointments).SingleOrDefaultAsync(item =>
                            item.IdempotencyKey == key, cancellationToken);
                    if (replay is not null)
                    {
                        if (!string.Equals(replay.RequestFingerprint, fingerprint,
                            StringComparison.Ordinal))
                        {
                            return Result.Failure<AppointmentModel>(
                                AppointmentErrors.ConcurrencyConflict);
                        }

                        await transaction.CommitAsync(cancellationToken);
                        return Result.Success(AppointmentInfrastructureSupport.Map(replay)
                            with { WasReplayed = true });
                    }

                    await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext,
                        input.DepartmentId, cancellationToken);
                    await TransactionalResourceLock.AcquirePatientAsync(_dbContext,
                        input.PatientId, cancellationToken);
                    await TransactionalResourceLock.AcquirePatientPackageAsync(_dbContext,
                        input.PatientPackageId.Value, cancellationToken);
                }
                Result<PreparedAppointment> prepared = await PrepareAsync(input, null,
                    actorUserId, null, cancellationToken);
                if (prepared.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<AppointmentModel>(prepared.Error);
                }

                await AcquireLocksAsync(prepared.Value, cancellationToken);
                if (!input.PatientPackageId.HasValue)
                {
                    await TransactionalResourceLock.AcquireDiscountScheduleReadAsync(
                        _dbContext, cancellationToken);
                    await _discountResolver.ApplyToAppointmentAsync(
                        prepared.Value.Appointment, input.DiscountOverride, actorUserId,
                        _timeProvider.GetUtcNow(), cancellationToken);
                }
                Result<FollowUp?> followUpResult = await LoadFollowUpAsync(input,
                    prepared.Value.Appointment, cancellationToken);
                if (followUpResult.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<AppointmentModel>(followUpResult.Error);
                }
                string? reason = await ConflictReasonAsync(prepared.Value.Appointment, null,
                    cancellationToken);
                if (reason is not null && prepared.Value.Appointment.ReservesResources)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    IReadOnlyCollection<AvailableSlot> alternatives =
                        await FindAlternativesAsync(prepared.Value, null, cancellationToken);
                    return Result.Failure<AppointmentModel>(
                        AppointmentErrors.Conflict(reason, alternatives));
                }

                Appointment appointment = prepared.Value.Appointment;
                _dbContext.Appointments.Add(appointment);
                await _dbContext.SaveChangesAsync(cancellationToken);
                if (prepared.Value.PatientPackage is PatientPackage patientPackage)
                {
                    PackageSessionLifecycle.Reserve(_dbContext, appointment, patientPackage,
                        actorUserId, _timeProvider.GetUtcNow());
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                if (followUpResult.Value is FollowUp followUp)
                {
                    DateTimeOffset now = _timeProvider.GetUtcNow();
                    followUp.Complete(appointment.Id, actorUserId, now);
                    _dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId,
                        "FollowUpConvertedToAppointment", nameof(FollowUp),
                        followUp.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        now, $"AppointmentId={appointment.Id}"));
                }
                AppointmentInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    AppointmentAuditActions.Created, appointment.Id,
                    _timeProvider.GetUtcNow());
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await LoadNavigationsAsync(appointment, cancellationToken);
                return Result.Success(AppointmentInfrastructureSupport.Map(appointment));
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<AppointmentModel>(
                AppointmentErrors.Validation(exception.Message));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<AppointmentModel>(
                AppointmentInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public async Task<Result<AppointmentModel>> UpdateAsync(long actorUserId,
        long appointmentId, AppointmentInput input, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await _dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                Appointment? appointment = await AppointmentInfrastructureSupport.Details(
                    _dbContext.Appointments).SingleOrDefaultAsync(item => item.Id == appointmentId,
                    cancellationToken);
                if (appointment is null)
                {
                    return Result.Failure<AppointmentModel>(AppointmentErrors.NotFound);
                }

                if (appointment.PatientId != input.PatientId)
                {
                    return Result.Failure<AppointmentModel>(AppointmentErrors.Validation(
                        "لا يمكن تغيير المريض داخل الحجز. ألغِ الحجز وأنشئ حجزًا جديدًا."));
                }

                if (appointment.PatientPackageId != input.PatientPackageId ||
                    appointment.PatientPackageId.HasValue &&
                    appointment.Services.Where(item =>
                        item.Status == AppointmentServiceStatus.Scheduled)
                        .Select(item => item.ServiceId).Order().SequenceEqual(
                            input.Services.Select(item => item.ServiceId).Order()) == false)
                {
                    return Result.Failure<AppointmentModel>(AppointmentErrors.Validation(
                        "لا يمكن تغيير الباقة أو خدماتها عند إعادة الجدولة."));
                }

                if (appointment.DepartmentId != input.DepartmentId)
                {
                    return Result.Failure<AppointmentModel>(AppointmentErrors.Validation(
                        "تغيير القسم يحتاج إلغاء الحجز وإنشاء حجز جديد."));
                }

                if (!AppointmentInfrastructureSupport.MatchesVersion(
                    appointment.RowVersion, rowVersion))
                {
                    return Result.Failure<AppointmentModel>(AppointmentErrors.ConcurrencyConflict);
                }

                if (await _dbContext.ApprovalRequests.AnyAsync(item =>
                    item.AppointmentId == appointmentId &&
                    item.Status == Domain.Approvals.ApprovalRequestStatus.Pending,
                    cancellationToken))
                {
                    return Result.Failure<AppointmentModel>(AppointmentErrors.NotEditable);
                }

                Dictionary<long, PreservedPricing> oldPricing = appointment.Services
                    .Where(item => item.Status == AppointmentServiceStatus.Scheduled)
                    .GroupBy(item => item.ServiceId).ToDictionary(group => group.Key,
                        group =>
                        {
                            Clinic.Domain.Appointments.AppointmentService item = group.First();
                            return new PreservedPricing(item.UnitPrice, item.DiscountId,
                                item.DiscountAmount, item.DiscountOverrideMode,
                                item.DiscountOverrideByAdminUserId,
                                item.DiscountOverrideReason);
                        });
                Dictionary<long, decimal> oldPrices = oldPricing.ToDictionary(item => item.Key,
                    item => item.Value.UnitPrice);
                Result<PreparedAppointment> prepared = await PrepareAsync(input, oldPrices,
                    actorUserId, appointmentId, cancellationToken);
                if (prepared.IsFailure)
                {
                    return Result.Failure<AppointmentModel>(prepared.Error);
                }

                await AcquireLocksAsync(prepared.Value, cancellationToken);
                string? reason = await ConflictReasonAsync(prepared.Value.Appointment,
                    appointmentId, cancellationToken);
                if (reason is not null && prepared.Value.Appointment.ReservesResources)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure<AppointmentModel>(AppointmentErrors.Conflict(reason,
                        await FindAlternativesAsync(prepared.Value, appointmentId,
                            cancellationToken)));
                }

                appointment.ReplaceSchedule(prepared.Value.Appointment.StartAt,
                    actorUserId, _timeProvider.GetUtcNow());
                foreach (PreparedLine line in prepared.Value.Lines)
                {
                    appointment.AddService(line.Service.Id, line.DoctorService.Id,
                        line.Service.DurationMinutes, line.Quantity, line.UnitPrice,
                        line.Devices.Select(item => (item.Id, item.DeviceId)).ToArray());
                }

                PreservedPricing? excluded = oldPricing.Values.FirstOrDefault(item =>
                    item.OverrideMode == DiscountOverrideMode.Exclude);
                if (excluded is not null)
                {
                    appointment.ExcludeDiscount(excluded.AdminUserId!.Value,
                        excluded.Reason);
                }
                else
                {
                    foreach (Clinic.Domain.Appointments.AppointmentService line in
                        appointment.Services.Where(item =>
                            item.Status == AppointmentServiceStatus.Scheduled))
                    {
                        if (oldPricing.TryGetValue(line.ServiceId,
                                out PreservedPricing? pricing) &&
                            pricing.DiscountId.HasValue)
                        {
                            appointment.ApplyDiscount(line.SequenceNumber,
                                pricing.DiscountId.Value, pricing.DiscountAmount,
                                pricing.OverrideMode, pricing.AdminUserId, pricing.Reason);
                        }
                    }
                    appointment.FinalizePricing();
                }

                if (prepared.Value.PatientPackage is PatientPackage patientPackage)
                {
                    appointment.CoverByPackage(patientPackage,
                        PackageSessionLifecycle.Prices(patientPackage,
                            input.Services.Select(item => item.ServiceId)),
                        appointment.IdempotencyKey!.Value,
                        appointment.RequestFingerprint!);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await PackageSessionLifecycle.ReplaceAsync(_dbContext, appointment,
                        patientPackage, actorUserId, _timeProvider.GetUtcNow(),
                        cancellationToken);
                }

                if (prepared.Value.Appointment.Status == AppointmentStatus.Suspended &&
                    appointment.Status != AppointmentStatus.Suspended)
                {
                    appointment.Suspend(actorUserId, _timeProvider.GetUtcNow());
                }
                else if (prepared.Value.Appointment.Status is AppointmentStatus.Booked or
                        AppointmentStatus.Confirmed &&
                    appointment.Status == AppointmentStatus.Suspended)
                {
                    appointment.Reactivate(actorUserId, _timeProvider.GetUtcNow());
                }

                AppointmentInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    AppointmentAuditActions.Updated, appointment.Id, _timeProvider.GetUtcNow());
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await LoadNavigationsAsync(appointment, cancellationToken);
                return Result.Success(AppointmentInfrastructureSupport.Map(appointment));
            });
        }
        catch (DomainException)
        {
            return Result.Failure<AppointmentModel>(AppointmentErrors.NotEditable);
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<AppointmentModel>(
                AppointmentInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    public Task<Result> CancelAsync(long actorUserId, long appointmentId, string? reason,
        byte[] rowVersion, CancellationToken cancellationToken) => ChangeStateAsync(
            actorUserId, appointmentId, rowVersion, AppointmentAuditActions.Cancelled,
            appointment => appointment.Cancel(actorUserId, _timeProvider.GetUtcNow(), reason),
            reason, cancellationToken);

    public Task<Result> CompleteAsync(long actorUserId, long appointmentId,
        byte[] rowVersion, CancellationToken cancellationToken) => ChangeStateAsync(
            actorUserId, appointmentId, rowVersion, AppointmentAuditActions.Completed,
            appointment => appointment.Complete(actorUserId, _timeProvider.GetUtcNow()),
            null, cancellationToken);

    public Task<Result> MarkNoShowAsync(long actorUserId, long appointmentId,
        byte[] rowVersion, CancellationToken cancellationToken) => ChangeStateAsync(
            actorUserId, appointmentId, rowVersion, AppointmentAuditActions.NoShow,
            appointment => appointment.MarkNoShow(actorUserId, _timeProvider.GetUtcNow()),
            null, cancellationToken);

    public async Task<Result<AppointmentModel>> GetAsync(long appointmentId,
        CancellationToken cancellationToken)
    {
        Appointment? appointment = await AppointmentInfrastructureSupport.Details(
            _dbContext.Appointments.AsNoTracking()).SingleOrDefaultAsync(
            item => item.Id == appointmentId, cancellationToken);
        return appointment is null
            ? Result.Failure<AppointmentModel>(AppointmentErrors.NotFound)
            : Result.Success(AppointmentInfrastructureSupport.Map(appointment));
    }

    public async Task<Result<IReadOnlyCollection<AppointmentModel>>> CalendarAsync(
        DateOnly clinicDate, long? departmentId, CancellationToken cancellationToken)
    {
        DateTimeOffset from = AppointmentInfrastructureSupport.ClinicDayStartUtc(clinicDate);
        DateTimeOffset to = AppointmentInfrastructureSupport.ClinicDayStartUtc(clinicDate.AddDays(1));
        IQueryable<Appointment> query = _dbContext.Appointments.AsNoTracking()
            .Where(item => item.StartAt < to && from < item.EndAt);
        if (departmentId.HasValue) query = query.Where(item => item.DepartmentId == departmentId);
        Appointment[] items = await AppointmentInfrastructureSupport.Details(query)
            .OrderBy(item => item.StartAt).ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<AppointmentModel>>(
            items.Select(AppointmentInfrastructureSupport.Map).ToArray());
    }

    public async Task<Result<AppointmentPage>> SearchAsync(AppointmentSearch search,
        CancellationToken cancellationToken)
    {
        IQueryable<Appointment> query = _dbContext.Appointments.AsNoTracking();
        if (search.From.HasValue) query = query.Where(item => item.EndAt > search.From);
        if (search.To.HasValue) query = query.Where(item => item.StartAt < search.To);
        if (search.DepartmentId.HasValue) query = query.Where(item => item.DepartmentId == search.DepartmentId);
        if (search.SpecializationId.HasValue) query = query.Where(item => item.Services.Any(line => line.Status != AppointmentServiceStatus.Superseded && line.Service.SpecializationId == search.SpecializationId));
        if (search.ServiceId.HasValue) query = query.Where(item => item.Services.Any(line => line.Status != AppointmentServiceStatus.Superseded && line.ServiceId == search.ServiceId));
        if (search.DoctorId.HasValue) query = query.Where(item => item.Services.Any(line => line.Status != AppointmentServiceStatus.Superseded && line.DoctorService.DoctorId == search.DoctorId));
        if (search.Status.HasValue) query = query.Where(item => item.Status == search.Status);
        if (search.PaymentStatus.HasValue) query = query.Where(item => item.PaymentStatus == search.PaymentStatus);
        if (search.CreatedByUserId.HasValue) query = query.Where(item => item.CreatedByUserId == search.CreatedByUserId);
        if (!string.IsNullOrWhiteSpace(search.PatientSearch))
        {
            string term = search.PatientSearch.Trim();
            query = query.Where(item => item.Patient.FullName.Contains(term) ||
                item.Patient.PrimaryPhoneNumber.Contains(term));
        }
        if (search.Gender.HasValue) query = query.Where(item => item.Patient.Gender == search.Gender);
        if (!string.IsNullOrWhiteSpace(search.Area)) query = query.Where(item => item.Patient.Area != null && item.Patient.Area.Contains(search.Area));

        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            _timeProvider.GetUtcNow(), AppointmentInfrastructureSupport.ClinicTimeZone).DateTime);
        if (search.MinimumAge.HasValue || search.MaximumAge.HasValue)
        {
            int min = search.MinimumAge ?? 0;
            int max = search.MaximumAge ?? 130;
            DateOnly latestBirth = today.AddYears(-min);
            DateOnly earliestBirth = today.AddYears(-max - 1).AddDays(1);
            query = query.Where(item =>
                (item.Patient.BirthDate != null && item.Patient.BirthDate >= earliestBirth && item.Patient.BirthDate <= latestBirth) ||
                (item.Patient.BirthDate == null && item.Patient.AgeRecordedAt != null &&
                 item.Patient.AgeAtRegistration +
                 EF.Functions.DateDiffYear(item.Patient.AgeRecordedAt.Value, today) -
                 (item.Patient.AgeRecordedAt.Value.AddYears(
                     EF.Functions.DateDiffYear(item.Patient.AgeRecordedAt.Value, today)) > today
                     ? 1 : 0) >= min &&
                 item.Patient.AgeAtRegistration +
                 EF.Functions.DateDiffYear(item.Patient.AgeRecordedAt.Value, today) -
                 (item.Patient.AgeRecordedAt.Value.AddYears(
                     EF.Functions.DateDiffYear(item.Patient.AgeRecordedAt.Value, today)) > today
                     ? 1 : 0) <= max));
        }

        int total = await query.CountAsync(cancellationToken);
        Appointment[] items = await AppointmentInfrastructureSupport.Details(query)
            .OrderByDescending(item => item.StartAt)
            .Skip((search.PageNumber - 1) * search.PageSize).Take(search.PageSize)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new AppointmentPage(items.Select(
            AppointmentInfrastructureSupport.Map).ToArray(), search.PageNumber,
            search.PageSize, total));
    }

    public async Task<Result<int>> RevalidateSuspendedAsync(long? actorUserId,
        long? departmentId, CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            IQueryable<Appointment> query = AppointmentInfrastructureSupport.Details(
                _dbContext.Appointments).Where(item =>
                    item.Status == AppointmentStatus.Suspended &&
                    item.EndAt > _timeProvider.GetUtcNow());
            if (departmentId.HasValue)
            {
                query = query.Where(item => item.DepartmentId == departmentId);
            }

            Appointment[] appointments = await query.OrderBy(item => item.StartAt)
                .ToArrayAsync(cancellationToken);
            int reactivated = 0;
            foreach (Appointment appointment in appointments)
            {
                await AcquireExistingLocksAsync(appointment, cancellationToken);
                bool stopped = await _dbContext.DepartmentClosures.AsNoTracking().AnyAsync(item =>
                    item.DepartmentId == appointment.DepartmentId && item.CancelledAt == null &&
                    item.StartAt < appointment.EndAt && appointment.StartAt < item.EndAt,
                    cancellationToken);
                if (stopped || await ConflictReasonAsync(appointment, appointment.Id,
                    cancellationToken, forceCheck: true) is not null) continue;
                if (appointment.PatientPackageId.HasValue)
                {
                    try
                    {
                        _ = await PackageSessionLifecycle.LoadUsableAsync(_dbContext,
                            appointment, appointment.PatientPackageId.Value,
                            cancellationToken);
                    }
                    catch (DomainException)
                    {
                        continue;
                    }

                    int reservations = await _dbContext.PackageSessionBookings.CountAsync(
                        item => item.AppointmentId == appointment.Id &&
                            item.Status == PackageSessionBookingStatus.Reserved,
                        cancellationToken);
                    if (reservations != appointment.Services.Count(item =>
                        item.Status == AppointmentServiceStatus.Scheduled)) continue;
                }

                DateTimeOffset now = _timeProvider.GetUtcNow();
                if (actorUserId.HasValue)
                {
                    appointment.Reactivate(actorUserId.Value, now);
                    AppointmentInfrastructureSupport.AddAudit(_dbContext, actorUserId.Value,
                        AppointmentAuditActions.Reactivated, appointment.Id, now);
                }
                else
                {
                    appointment.ReactivateBySystem(now);
                    AppointmentInfrastructureSupport.AddSystemAudit(_dbContext,
                        AppointmentAuditActions.Reactivated, appointment.Id, now);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                reactivated++;
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success(reactivated);
        });
    }

    public async Task<Result<AppointmentModel>> TransferDoctorAsync(long actorUserId,
        long appointmentId, long appointmentServiceId, long doctorId, string reason,
        byte[] rowVersion, CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction = await _dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                Appointment? appointment = await AppointmentInfrastructureSupport.Details(
                    _dbContext.Appointments).SingleOrDefaultAsync(
                    item => item.Id == appointmentId, cancellationToken);
                if (appointment is null)
                    return Result.Failure<AppointmentModel>(AppointmentErrors.NotFound);
                if (!AppointmentInfrastructureSupport.MatchesVersion(
                    appointment.RowVersion, rowVersion))
                    return Result.Failure<AppointmentModel>(AppointmentErrors.ConcurrencyConflict);
                Clinic.Domain.Appointments.AppointmentService? line = appointment.Services
                    .SingleOrDefault(item => item.Id == appointmentServiceId &&
                        item.Status == AppointmentServiceStatus.Scheduled);
                if (line is null)
                    return Result.Failure<AppointmentModel>(AppointmentErrors.ResourceNotFound);

                await TransactionalResourceLock.AcquireDoctorAsync(_dbContext, doctorId,
                    cancellationToken);
                DoctorService? assignment = await _dbContext.DoctorServices
                    .Include(item => item.Doctor).SingleOrDefaultAsync(item =>
                        item.DoctorId == doctorId && item.ServiceId == line.ServiceId &&
                        item.DepartmentId == appointment.DepartmentId && item.IsActive &&
                        item.Doctor.IsActive && !item.Doctor.IsArchived, cancellationToken);
                if (assignment is null || !await DoctorAvailableAsync(doctorId,
                    line.SegmentStartAt, line.SegmentEndAt, cancellationToken))
                    return Result.Failure<AppointmentModel>(AppointmentErrors.ResourceNotFound);

                bool overlap = await _dbContext.AppointmentServices.AsNoTracking().AnyAsync(item =>
                    item.AppointmentId != appointmentId &&
                    item.DoctorService.DoctorId == doctorId &&
                    item.Status == AppointmentServiceStatus.Scheduled &&
                    (item.Appointment.Status == AppointmentStatus.Booked ||
                     item.Appointment.Status == AppointmentStatus.Confirmed) &&
                    item.SegmentStartAt < line.SegmentEndAt &&
                    line.SegmentStartAt < item.SegmentEndAt, cancellationToken);
                if (overlap)
                    return Result.Failure<AppointmentModel>(AppointmentErrors.Conflict(
                        "الطبيب البديل لديه حجز متداخل.", []));

                DateTimeOffset changedAt = _timeProvider.GetUtcNow();
                line = appointment.TransferDoctor(line.Id, assignment.Id, actorUserId, changedAt);
                _dbContext.Entry(line).Reference(item => item.DoctorService).CurrentValue = assignment;
                AppointmentInfrastructureSupport.AddAudit(_dbContext, actorUserId,
                    AppointmentAuditActions.DoctorTransferred, appointment.Id,
                    changedAt, reason);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(AppointmentInfrastructureSupport.Map(appointment));
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<AppointmentModel>(
                AppointmentInfrastructureSupport.MapDatabaseFailure(exception));
        }
    }

    private async Task<Result<PreparedAppointment>> PrepareAsync(AppointmentInput input,
        Dictionary<long, decimal>? preservedPrices, long actorUserId,
        long? existingAppointmentId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset startAt = input.StartAt.ToUniversalTime();
        if (startAt < _timeProvider.GetUtcNow())
        {
            return Result.Failure<PreparedAppointment>(AppointmentErrors.Validation(
                "لا يمكن إنشاء حجز في وقت مضى."));
        }

        if (!await _dbContext.Patients.AsNoTracking().AnyAsync(
            item => item.Id == input.PatientId && !item.IsArchived, cancellationToken))
        {
            return Result.Failure<PreparedAppointment>(AppointmentErrors.PatientNotFound);
        }

        Room? room = await _dbContext.Rooms.Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.DepartmentId == input.DepartmentId &&
                item.IsActive && !item.IsArchived && !item.Department.IsArchived,
                cancellationToken);
        if (room is null)
        {
            return Result.Failure<PreparedAppointment>(AppointmentErrors.DepartmentNotFound);
        }

        long[] serviceIds = input.Services.Select(item => item.ServiceId).Distinct().ToArray();
        Dictionary<long, Service> services = await _dbContext.Services
            .Include(item => item.DeviceAssignments).ThenInclude(item => item.Device)
            .Where(item => serviceIds.Contains(item.Id) && item.DepartmentId == input.DepartmentId &&
                item.IsActive && !item.IsArchived).ToDictionaryAsync(item => item.Id,
                cancellationToken);
        if (services.Count != serviceIds.Length)
        {
            return Result.Failure<PreparedAppointment>(AppointmentErrors.ResourceNotFound);
        }

        long[] doctorIds = input.Services.Select(item => item.DoctorId).Distinct().ToArray();
        DoctorService[] assignments = await _dbContext.DoctorServices
            .Include(item => item.Doctor)
            .Where(item => doctorIds.Contains(item.DoctorId) && serviceIds.Contains(item.ServiceId) &&
                item.DepartmentId == input.DepartmentId && item.IsActive && item.Doctor.IsActive &&
                !item.Doctor.IsArchived).ToArrayAsync(cancellationToken);

        Appointment appointment = Appointment.Create(input.PatientId, room.Id,
            input.DepartmentId, startAt, suspended: false, actorUserId,
            _timeProvider.GetUtcNow());
        PatientPackage? patientPackage = null;
        IReadOnlyDictionary<long, decimal>? packagePrices = null;
        if (input.PatientPackageId.HasValue)
        {
            patientPackage = await PackageSessionLifecycle.LoadUsableAsync(_dbContext,
                appointment, input.PatientPackageId.Value, cancellationToken, serviceIds);
            await PackageSessionLifecycle.EnsureBalanceAsync(_dbContext, patientPackage,
                serviceIds, existingAppointmentId, cancellationToken);
            packagePrices = PackageSessionLifecycle.Prices(patientPackage, serviceIds);
        }
        List<PreparedLine> lines = [];
        foreach (AppointmentLineInput inputLine in input.Services)
        {
            Service service = services[inputLine.ServiceId];
            DoctorService? doctorService = assignments.SingleOrDefault(item =>
                item.DoctorId == inputLine.DoctorId && item.ServiceId == inputLine.ServiceId);
            if (doctorService is null || service.PricingMode == PricingMode.Fixed && inputLine.Quantity != 1)
            {
                return Result.Failure<PreparedAppointment>(AppointmentErrors.ResourceNotFound);
            }

            long[] selected = inputLine.OptionalDeviceIds.Distinct().ToArray();
            ServiceDevice[] devices = service.DeviceAssignments.Where(item => item.IsActive &&
                (item.IsRequired || selected.Contains(item.DeviceId))).ToArray();
            if (selected.Any(id => !devices.Any(item => item.DeviceId == id && !item.IsRequired)) ||
                devices.Any(item => item.Device.IsArchived || !item.Device.IsActive))
            {
                return Result.Failure<PreparedAppointment>(AppointmentErrors.ResourceNotFound);
            }

            decimal unitPrice = preservedPrices is not null &&
                preservedPrices.TryGetValue(service.Id, out decimal preservedPrice)
                ? preservedPrice
                : packagePrices?.GetValueOrDefault(service.Id) ?? service.CurrentUnitPrice;
            appointment.AddService(service.Id, doctorService.Id, service.DurationMinutes,
                inputLine.Quantity, unitPrice, devices.Select(item => (item.Id, item.DeviceId)).ToArray());
            lines.Add(new PreparedLine(service, doctorService, inputLine.Quantity, unitPrice, devices));
        }

        if (patientPackage is not null)
        {
            appointment.CoverByPackage(patientPackage, packagePrices!,
                input.IdempotencyKey ?? Guid.NewGuid(), input.IdempotencyKey.HasValue
                    ? Fingerprint(actorUserId, input) : new string('0', 64));
        }

        bool stopped = await _dbContext.DepartmentClosures.AsNoTracking().AnyAsync(item =>
            item.DepartmentId == input.DepartmentId && item.CancelledAt == null &&
            item.StartAt < appointment.EndAt && appointment.StartAt < item.EndAt,
            cancellationToken);
        if (stopped) appointment.Suspend(actorUserId, _timeProvider.GetUtcNow());
        return Result.Success(new PreparedAppointment(appointment, room, lines, patientPackage));
    }

    private async Task<string?> ConflictReasonAsync(Appointment appointment,
        long? excludedAppointmentId, CancellationToken cancellationToken,
        bool forceCheck = false)
    {
        if (!forceCheck && appointment.Status == AppointmentStatus.Suspended) return null;
        if (await _dbContext.Appointments.AsNoTracking().AnyAsync(item =>
            (!excludedAppointmentId.HasValue || item.Id != excludedAppointmentId) &&
            item.RoomId == appointment.RoomId &&
            (item.Status == AppointmentStatus.Booked || item.Status == AppointmentStatus.Confirmed) &&
            item.StartAt < appointment.EndAt && appointment.StartAt < item.EndAt,
            cancellationToken)) return "الغرفة محجوزة في هذا الوقت.";

        foreach (Clinic.Domain.Appointments.AppointmentService line in appointment.Services.Where(
            item => item.Status == AppointmentServiceStatus.Scheduled))
        {
            long doctorId = await _dbContext.DoctorServices.Where(item => item.Id == line.DoctorServiceId)
                .Select(item => item.DoctorId).SingleAsync(cancellationToken);
            if (!await DoctorAvailableAsync(doctorId, line.SegmentStartAt, line.SegmentEndAt,
                cancellationToken)) return "الطبيب غير متاح في هذا الوقت.";
            if (await _dbContext.AppointmentServices.AsNoTracking().AnyAsync(item =>
                (!excludedAppointmentId.HasValue || item.AppointmentId != excludedAppointmentId) &&
                item.DoctorService.DoctorId == doctorId && item.Status == AppointmentServiceStatus.Scheduled &&
                (item.Appointment.Status == AppointmentStatus.Booked || item.Appointment.Status == AppointmentStatus.Confirmed) &&
                item.SegmentStartAt < line.SegmentEndAt && line.SegmentStartAt < item.SegmentEndAt,
                cancellationToken)) return "الطبيب لديه حجز متداخل.";

            foreach (AppointmentDevice device in line.Devices)
            {
                if (await _dbContext.AppointmentDevices.AsNoTracking().AnyAsync(item =>
                    (!excludedAppointmentId.HasValue || item.AppointmentService.AppointmentId != excludedAppointmentId) &&
                    item.DeviceId == device.DeviceId &&
                    item.AppointmentService.Status == AppointmentServiceStatus.Scheduled &&
                    (item.AppointmentService.Appointment.Status == AppointmentStatus.Booked || item.AppointmentService.Appointment.Status == AppointmentStatus.Confirmed) &&
                    item.ReservedFrom < device.ReservedTo && device.ReservedFrom < item.ReservedTo,
                    cancellationToken)) return "أحد الأجهزة المطلوبة محجوز في هذا الوقت.";
            }
        }

        return null;
    }

    private async Task<bool> DoctorAvailableAsync(long doctorId, DateTimeOffset from,
        DateTimeOffset to, CancellationToken cancellationToken)
    {
        DateTimeOffset localFrom = TimeZoneInfo.ConvertTime(from,
            AppointmentInfrastructureSupport.ClinicTimeZone);
        DateOnly date = DateOnly.FromDateTime(localFrom.DateTime);

        DoctorScheduleOverride[] overrides = await _dbContext.DoctorExceptions.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.ExceptionDate == date &&
                item.CancelledAt == null).ToArrayAsync(cancellationToken);
        DoctorSchedule[] schedules = await _dbContext.DoctorSchedules.AsNoTracking()
            .Where(item => item.DoctorId == doctorId && item.IsActive &&
                item.EffectiveFrom <= date && (!item.EffectiveTo.HasValue || item.EffectiveTo >= date))
            .ToArrayAsync(cancellationToken);
        return AppointmentInfrastructureSupport.IsDoctorAvailable(from, to, schedules, overrides);
    }

    private async Task<IReadOnlyCollection<AvailableSlot>> FindAlternativesAsync(
        PreparedAppointment prepared, long? excludedAppointmentId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset firstCandidate = prepared.Appointment.StartAt.AddMinutes(15);
        DateTimeOffset limit = prepared.Appointment.StartAt.AddDays(30);
        TimeSpan duration = prepared.Appointment.EndAt - prepared.Appointment.StartAt;
        DateTimeOffset windowEnd = limit.Add(duration);
        long[] doctorIds = prepared.Lines.Select(item => item.DoctorService.DoctorId)
            .Distinct().ToArray();
        long[] deviceIds = prepared.Lines.SelectMany(item => item.Devices)
            .Select(item => item.DeviceId).Distinct().ToArray();
        DateOnly firstDate = ClinicDate(firstCandidate);
        DateOnly lastDate = ClinicDate(windowEnd);

        TimeInterval[] roomBusy = await _dbContext.Appointments.AsNoTracking()
            .Where(item => (!excludedAppointmentId.HasValue || item.Id != excludedAppointmentId) &&
                item.RoomId == prepared.Room.Id &&
                (item.Status == AppointmentStatus.Booked ||
                 item.Status == AppointmentStatus.Confirmed) &&
                item.StartAt < windowEnd && firstCandidate < item.EndAt)
            .Select(item => new TimeInterval(item.StartAt, item.EndAt))
            .ToArrayAsync(cancellationToken);
        ResourceInterval[] doctorBusy = await _dbContext.AppointmentServices.AsNoTracking()
            .Where(item => (!excludedAppointmentId.HasValue ||
                    item.AppointmentId != excludedAppointmentId) &&
                doctorIds.Contains(item.DoctorService.DoctorId) &&
                item.Status == AppointmentServiceStatus.Scheduled &&
                (item.Appointment.Status == AppointmentStatus.Booked ||
                 item.Appointment.Status == AppointmentStatus.Confirmed) &&
                item.SegmentStartAt < windowEnd && firstCandidate < item.SegmentEndAt)
            .Select(item => new ResourceInterval(item.DoctorService.DoctorId,
                item.SegmentStartAt, item.SegmentEndAt))
            .ToArrayAsync(cancellationToken);
        ResourceInterval[] deviceBusy = deviceIds.Length == 0
            ? []
            : await _dbContext.AppointmentDevices.AsNoTracking()
                .Where(item => (!excludedAppointmentId.HasValue ||
                        item.AppointmentService.AppointmentId != excludedAppointmentId) &&
                    deviceIds.Contains(item.DeviceId) &&
                    item.AppointmentService.Status == AppointmentServiceStatus.Scheduled &&
                    (item.AppointmentService.Appointment.Status == AppointmentStatus.Booked ||
                     item.AppointmentService.Appointment.Status == AppointmentStatus.Confirmed) &&
                    item.ReservedFrom < windowEnd && firstCandidate < item.ReservedTo)
                .Select(item => new ResourceInterval(item.DeviceId,
                    item.ReservedFrom, item.ReservedTo))
                .ToArrayAsync(cancellationToken);
        DepartmentClosure[] closures = await _dbContext.DepartmentClosures.AsNoTracking()
            .Where(item => item.DepartmentId == prepared.Appointment.DepartmentId &&
                item.CancelledAt == null && item.StartAt < windowEnd &&
                firstCandidate < item.EndAt).ToArrayAsync(cancellationToken);
        DoctorSchedule[] schedules = await _dbContext.DoctorSchedules.AsNoTracking()
            .Where(item => doctorIds.Contains(item.DoctorId) && item.IsActive &&
                item.EffectiveFrom <= lastDate &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo >= firstDate))
            .ToArrayAsync(cancellationToken);
        DoctorScheduleOverride[] exceptions = await _dbContext.DoctorExceptions.AsNoTracking()
            .Where(item => doctorIds.Contains(item.DoctorId) && item.CancelledAt == null &&
                item.ExceptionDate >= firstDate && item.ExceptionDate <= lastDate)
            .ToArrayAsync(cancellationToken);
        Dictionary<long, DoctorSchedule[]> schedulesByDoctor = schedules
            .GroupBy(item => item.DoctorId).ToDictionary(group => group.Key, group => group.ToArray());
        Dictionary<long, DoctorScheduleOverride[]> exceptionsByDoctor = exceptions
            .GroupBy(item => item.DoctorId).ToDictionary(group => group.Key, group => group.ToArray());

        List<AvailableSlot> alternatives = [];
        for (DateTimeOffset candidate = firstCandidate;
             candidate <= limit && alternatives.Count < 5;
             candidate = candidate.AddMinutes(15))
        {
            DateTimeOffset candidateEnd = candidate.Add(duration);
            if (Overlaps(roomBusy, candidate, candidateEnd) ||
                closures.Any(item => item.StartAt < candidateEnd && candidate < item.EndAt)) continue;

            DateTimeOffset segmentStart = candidate;
            bool available = true;
            foreach (PreparedLine line in prepared.Lines)
            {
                DateTimeOffset segmentEnd = segmentStart.AddMinutes(line.Service.DurationMinutes);
                long doctorId = line.DoctorService.DoctorId;
                if (!AppointmentInfrastructureSupport.IsDoctorAvailable(segmentStart, segmentEnd,
                        schedulesByDoctor.GetValueOrDefault(doctorId) ?? [],
                        exceptionsByDoctor.GetValueOrDefault(doctorId) ?? []) ||
                    Overlaps(doctorBusy, doctorId, segmentStart, segmentEnd) ||
                    line.Devices.Any(device => Overlaps(deviceBusy, device.DeviceId,
                        segmentStart, segmentEnd)))
                {
                    available = false;
                    break;
                }
                segmentStart = segmentEnd;
            }

            if (available) alternatives.Add(new AvailableSlot(candidate, candidateEnd));
        }
        return alternatives;
    }

    private static DateOnly ClinicDate(DateTimeOffset value) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(value, AppointmentInfrastructureSupport.ClinicTimeZone).DateTime);

    private static bool Overlaps(IEnumerable<TimeInterval> intervals,
        DateTimeOffset from, DateTimeOffset to) => intervals.Any(item =>
            item.StartAt < to && from < item.EndAt);

    private static bool Overlaps(IEnumerable<ResourceInterval> intervals, long resourceId,
        DateTimeOffset from, DateTimeOffset to) => intervals.Any(item =>
            item.ResourceId == resourceId && item.StartAt < to && from < item.EndAt);

    private async Task AcquireLocksAsync(PreparedAppointment prepared,
        CancellationToken cancellationToken)
    {
        await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext,
            prepared.Appointment.DepartmentId, cancellationToken);
        await TransactionalResourceLock.AcquirePatientAsync(_dbContext,
            prepared.Appointment.PatientId, cancellationToken);
        if (prepared.Appointment.PatientPackageId.HasValue)
        {
            await TransactionalResourceLock.AcquirePatientPackageAsync(_dbContext,
                prepared.Appointment.PatientPackageId.Value, cancellationToken);
        }
        await TransactionalResourceLock.AcquireRoomAsync(_dbContext, prepared.Room.Id,
            cancellationToken);
        foreach (long doctorId in prepared.Lines.Select(item => item.DoctorService.DoctorId).Distinct().Order())
            await TransactionalResourceLock.AcquireDoctorAsync(_dbContext, doctorId, cancellationToken);
        foreach (long deviceId in prepared.Lines.SelectMany(item => item.Devices).Select(item => item.DeviceId).Distinct().Order())
            await TransactionalResourceLock.AcquireDeviceAsync(_dbContext, deviceId, cancellationToken);
    }

    private async Task<Result<FollowUp?>> LoadFollowUpAsync(AppointmentInput input,
        Appointment appointment, CancellationToken cancellationToken)
    {
        if (input.FollowUp is null)
            return Result.Success<FollowUp?>(null);

        await TransactionalResourceLock.AcquireFollowUpAsync(_dbContext,
            input.FollowUp.FollowUpId, cancellationToken);
        FollowUp? followUp = await _dbContext.FollowUps
            .Include(item => item.Prescription)
            .SingleOrDefaultAsync(item => item.Id == input.FollowUp.FollowUpId,
                cancellationToken);
        if (followUp is null)
            return Result.Failure<FollowUp?>(AppointmentErrors.FollowUpNotFound);

        byte[] expectedVersion = Convert.FromBase64String(input.FollowUp.RowVersion);
        if (!AppointmentInfrastructureSupport.MatchesVersion(followUp.RowVersion,
            expectedVersion))
            return Result.Failure<FollowUp?>(AppointmentErrors.ConcurrencyConflict);
        if (followUp.Status != FollowUpStatus.Due ||
            followUp.Prescription.Status != PrescriptionStatus.Finalized)
            return Result.Failure<FollowUp?>(AppointmentErrors.FollowUpNotFound);
        if (followUp.PatientId != appointment.PatientId ||
            followUp.DepartmentId != appointment.DepartmentId)
            return Result.Failure<FollowUp?>(AppointmentErrors.Validation(
                "يجب أن يخص حجز المتابعة نفس المريض والقسم."));
        if (appointment.Status == AppointmentStatus.Suspended)
            return Result.Failure<FollowUp?>(AppointmentErrors.Validation(
                "لا يمكن تحويل المتابعة إلى حجز موقوف. اختر موعدًا متاحًا."));

        return Result.Success<FollowUp?>(followUp);
    }

    private async Task AcquireExistingLocksAsync(Appointment appointment,
        CancellationToken cancellationToken)
    {
        await TransactionalResourceLock.AcquireDepartmentAsync(_dbContext,
            appointment.DepartmentId, cancellationToken);
        await TransactionalResourceLock.AcquirePatientAsync(_dbContext,
            appointment.PatientId, cancellationToken);
        if (appointment.PatientPackageId.HasValue)
        {
            await TransactionalResourceLock.AcquirePatientPackageAsync(_dbContext,
                appointment.PatientPackageId.Value, cancellationToken);
        }
        await TransactionalResourceLock.AcquireRoomAsync(_dbContext, appointment.RoomId,
            cancellationToken);
        foreach (long doctorId in appointment.Services
            .Where(item => item.Status == AppointmentServiceStatus.Scheduled)
            .Select(item => item.DoctorService.DoctorId).Distinct().Order())
            await TransactionalResourceLock.AcquireDoctorAsync(_dbContext, doctorId, cancellationToken);
        foreach (long deviceId in appointment.Services.SelectMany(item => item.Devices)
            .Where(item => item.AppointmentService.Status == AppointmentServiceStatus.Scheduled)
            .Select(item => item.DeviceId).Distinct().Order())
            await TransactionalResourceLock.AcquireDeviceAsync(_dbContext, deviceId, cancellationToken);
    }

    private async Task<Result> ChangeStateAsync(long actorUserId, long appointmentId,
        byte[] rowVersion, string action, Action<Appointment> mutation, string? reason,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await _dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                Appointment? appointment = await AppointmentInfrastructureSupport.Details(
                    _dbContext.Appointments).SingleOrDefaultAsync(item =>
                        item.Id == appointmentId, cancellationToken);
                if (appointment is null) return Result.Failure(AppointmentErrors.NotFound);
                await AcquireExistingLocksAsync(appointment, cancellationToken);
                if (!AppointmentInfrastructureSupport.MatchesVersion(
                    appointment.RowVersion, rowVersion))
                    return Result.Failure(AppointmentErrors.ConcurrencyConflict);
                if (action != AppointmentAuditActions.Cancelled &&
                    await _dbContext.ApprovalRequests.AnyAsync(item =>
                        item.AppointmentId == appointmentId &&
                        item.Status == Domain.Approvals.ApprovalRequestStatus.Pending,
                        cancellationToken))
                    return Result.Failure(AppointmentErrors.NotEditable);

                DateTimeOffset now = _timeProvider.GetUtcNow();
                if (appointment.PatientPackageId.HasValue &&
                    action == AppointmentAuditActions.Completed)
                    await PackageSessionLifecycle.ConsumeAsync(_dbContext, appointment,
                        actorUserId, now, cancellationToken);
                else if (appointment.PatientPackageId.HasValue &&
                    action == AppointmentAuditActions.NoShow)
                    await PackageSessionLifecycle.ReleaseAsync(_dbContext, appointment.Id,
                        actorUserId, now, cancellationToken);

                mutation(appointment);
                AppointmentInfrastructureSupport.AddAudit(_dbContext, actorUserId, action,
                    appointmentId, now, reason);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success();
            });
        }
        catch (DomainException)
        {
            return Result.Failure(AppointmentErrors.NotEditable);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(AppointmentErrors.ConcurrencyConflict);
        }
    }

    private async Task LoadNavigationsAsync(Appointment appointment,
        CancellationToken cancellationToken)
    {
        await _dbContext.Entry(appointment).Reference(item => item.Patient).LoadAsync(cancellationToken);
        await _dbContext.Entry(appointment).Reference(item => item.Room).LoadAsync(cancellationToken);
        await _dbContext.Entry(appointment.Room).Reference(item => item.Department).LoadAsync(cancellationToken);
        foreach (Clinic.Domain.Appointments.AppointmentService line in appointment.Services)
        {
            await _dbContext.Entry(line).Reference(item => item.Service).LoadAsync(cancellationToken);
            await _dbContext.Entry(line).Reference(item => item.DoctorService).LoadAsync(cancellationToken);
            await _dbContext.Entry(line.DoctorService).Reference(item => item.Doctor).LoadAsync(cancellationToken);
        }
    }

    private static string Fingerprint(long actorUserId, AppointmentInput input)
    {
        string services = string.Join(';', input.Services.Select(item =>
            $"{item.ServiceId}:{item.DoctorId}:{item.Quantity}:" +
            string.Join(',', item.OptionalDeviceIds.Order())));
        string value = $"{actorUserId}|{input.PatientId}|{input.DepartmentId}|" +
            $"{input.StartAt.ToUniversalTime():O}|{input.PatientPackageId}|{services}|" +
            $"{input.DiscountOverride?.Mode}|{input.DiscountOverride?.DiscountId}|" +
            $"{input.DiscountOverride?.Reason?.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed record PreparedAppointment(Appointment Appointment, Room Room,
        IReadOnlyCollection<PreparedLine> Lines, PatientPackage? PatientPackage);
    private sealed record PreparedLine(Service Service, DoctorService DoctorService,
        int Quantity, decimal UnitPrice, IReadOnlyCollection<ServiceDevice> Devices);
    private sealed record TimeInterval(DateTimeOffset StartAt, DateTimeOffset EndAt);
    private sealed record ResourceInterval(long ResourceId, DateTimeOffset StartAt,
        DateTimeOffset EndAt);
    private sealed record PreservedPricing(decimal UnitPrice, long? DiscountId,
        decimal DiscountAmount, DiscountOverrideMode? OverrideMode, long? AdminUserId,
        string? Reason);
}
