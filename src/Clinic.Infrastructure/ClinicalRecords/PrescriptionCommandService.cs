namespace Clinic.Infrastructure.ClinicalRecords;

public sealed class PrescriptionCommandService(ClinicDbContext dbContext,
    TimeProvider timeProvider, IClinicalRecordQueryService queryService)
    : IPrescriptionCommandService
{
    public async Task<Result<PrescriptionModel>> CreateDraftAsync(long actorUserId,
        long appointmentServiceId, PrescriptionContentInput content,
        CancellationToken cancellationToken)
    {
        ClinicalTarget? candidate = await FindTargetAsync(appointmentServiceId,
            cancellationToken);
        if (!IsEligible(candidate))
            return Result.Failure<PrescriptionModel>(
                ClinicalRecordErrors.AppointmentServiceNotFound);

        DateOnly visitDate = ClinicDate(candidate!.AppointmentStartAt);
        Result<PrescriptionContent> resolved = ResolveContent(content, visitDate);
        if (resolved.IsFailure)
            return Result.Failure<PrescriptionModel>(resolved.Error);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await TransactionalResourceLock.AcquirePatientAsync(dbContext,
                    candidate.PatientId, cancellationToken);
                await TransactionalResourceLock.AcquireAppointmentAsync(dbContext,
                    candidate.AppointmentId, cancellationToken);
                ClinicalTarget? target = await FindTargetAsync(appointmentServiceId,
                    cancellationToken);
                if (!IsEligible(target))
                    return Result.Failure<PrescriptionModel>(
                        ClinicalRecordErrors.AppointmentServiceNotFound);
                if (await dbContext.Prescriptions.AnyAsync(item =>
                    item.AppointmentServiceId == appointmentServiceId, cancellationToken))
                    return Result.Failure<PrescriptionModel>(ClinicalRecordErrors.Conflict(
                        "تم إنشاء روشتة لهذه الخدمة بالفعل."));

                DateTimeOffset now = timeProvider.GetUtcNow();
                Prescription prescription = Prescription.Create(appointmentServiceId,
                    actorUserId, now, resolved.Value);
                dbContext.Prescriptions.Add(prescription);
                await dbContext.SaveChangesAsync(cancellationToken);
                prescription.SelectCurrentRevision(prescription.Revisions.Single());
                AddAudit(actorUserId, "clinical.prescription_created", prescription.Id,
                    now, null);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return await GetSavedPrescriptionAsync(prescription.Id, cancellationToken);
            });
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<PrescriptionModel>(MapWriteFailure(exception));
        }
    }

    public async Task<Result<PrescriptionModel>> SaveDraftAsync(long actorUserId,
        long prescriptionId, PrescriptionContentInput content, byte[] rowVersion,
        CancellationToken cancellationToken) => await ChangePrescriptionAsync(
            actorUserId, prescriptionId, rowVersion, content, null,
            PrescriptionChange.SaveDraft, cancellationToken);

    public async Task<Result<PrescriptionModel>> CorrectAsync(long actorUserId,
        long prescriptionId, PrescriptionContentInput content, string reason,
        bool matchesDoctorPrescription, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        if (!matchesDoctorPrescription)
            return Result.Failure<PrescriptionModel>(ClinicalRecordErrors.Validation(
                "يجب تأكيد مطابقة التصحيح لروشتة الطبيب."));
        return await ChangePrescriptionAsync(actorUserId, prescriptionId, rowVersion,
            content, reason, PrescriptionChange.Correct, cancellationToken);
    }

    public async Task<Result<PrescriptionModel>> FinalizeAsync(long actorUserId,
        long prescriptionId, bool matchesDoctorPrescription, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        long? patientId = await FindPrescriptionPatientIdAsync(prescriptionId,
            cancellationToken);
        if (!patientId.HasValue)
            return Result.Failure<PrescriptionModel>(
                ClinicalRecordErrors.PrescriptionNotFound);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await LockPrescriptionAsync(patientId.Value, prescriptionId,
                    cancellationToken);
                Result<Prescription> loaded = await LoadForWriteAsync(prescriptionId,
                    rowVersion, cancellationToken);
                if (loaded.IsFailure) return Result.Failure<PrescriptionModel>(loaded.Error);
                Prescription prescription = loaded.Value;
                DateTimeOffset now = timeProvider.GetUtcNow();
                prescription.Finalize(actorUserId, now, matchesDoctorPrescription);
                DateOnly? returnDate = prescription.CurrentRevision!.ReturnDate;
                if (returnDate.HasValue)
                {
                    ClinicalTarget target = await FindTargetAsync(
                        prescription.AppointmentServiceId, cancellationToken)
                        ?? throw new InvalidOperationException("Missing prescription target.");
                    dbContext.FollowUps.Add(FollowUp.Create(prescription.Id,
                        target.PatientId, target.DepartmentId, returnDate.Value,
                        actorUserId, now));
                }
                AddAudit(actorUserId, "clinical.prescription_finalized",
                    prescription.Id, now, null);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return await GetSavedPrescriptionAsync(prescription.Id, cancellationToken);
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<PrescriptionModel>(
                ClinicalRecordErrors.Conflict(exception.Message));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<PrescriptionModel>(MapWriteFailure(exception));
        }
    }

    public async Task<Result<PrescriptionModel>> VoidAsync(long actorUserId,
        long prescriptionId, string reason, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        long? patientId = await FindPrescriptionPatientIdAsync(prescriptionId,
            cancellationToken);
        if (!patientId.HasValue)
            return Result.Failure<PrescriptionModel>(
                ClinicalRecordErrors.PrescriptionNotFound);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await LockPrescriptionAsync(patientId.Value, prescriptionId,
                    cancellationToken);
                Result<Prescription> loaded = await LoadForWriteAsync(prescriptionId,
                    rowVersion, cancellationToken);
                if (loaded.IsFailure) return Result.Failure<PrescriptionModel>(loaded.Error);
                Prescription prescription = loaded.Value;
                DateTimeOffset now = timeProvider.GetUtcNow();
                prescription.Void(actorUserId, now, reason);
                FollowUp? followUp = await dbContext.FollowUps.SingleOrDefaultAsync(item =>
                    item.PrescriptionId == prescription.Id, cancellationToken);
                if (followUp?.Status == FollowUpStatus.Due)
                    followUp.Cancel(actorUserId, now, "إبطال الروشتة");
                AddAudit(actorUserId, "clinical.prescription_voided", prescription.Id,
                    now, reason);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return await GetSavedPrescriptionAsync(prescription.Id, cancellationToken);
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<PrescriptionModel>(
                ClinicalRecordErrors.Conflict(exception.Message));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<PrescriptionModel>(MapWriteFailure(exception));
        }
    }

    private async Task<Result<PrescriptionModel>> ChangePrescriptionAsync(
        long actorUserId, long prescriptionId, byte[] rowVersion,
        PrescriptionContentInput input, string? reason, PrescriptionChange change,
        CancellationToken cancellationToken)
    {
        long? patientId = await FindPrescriptionPatientIdAsync(prescriptionId,
            cancellationToken);
        if (!patientId.HasValue)
            return Result.Failure<PrescriptionModel>(
                ClinicalRecordErrors.PrescriptionNotFound);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await LockPrescriptionAsync(patientId.Value, prescriptionId,
                    cancellationToken);
                Result<Prescription> loaded = await LoadForWriteAsync(prescriptionId,
                    rowVersion, cancellationToken);
                if (loaded.IsFailure) return Result.Failure<PrescriptionModel>(loaded.Error);
                Prescription prescription = loaded.Value;
                DateOnly visitDate = ClinicDate(
                    prescription.AppointmentService.Appointment.StartAt);
                Result<PrescriptionContent> content = ResolveContent(input, visitDate);
                if (content.IsFailure)
                    return Result.Failure<PrescriptionModel>(content.Error);
                DateTimeOffset now = timeProvider.GetUtcNow();
                PrescriptionRevision revision = change == PrescriptionChange.SaveDraft
                    ? prescription.SaveDraft(actorUserId, now, content.Value)
                    : prescription.Correct(actorUserId, now, reason!, content.Value);
                await dbContext.SaveChangesAsync(cancellationToken);
                prescription.SelectCurrentRevision(revision);
                if (change == PrescriptionChange.Correct)
                    await SynchronizeFollowUpAsync(prescription, content.Value.ReturnDate,
                        actorUserId, now, cancellationToken);
                AddAudit(actorUserId, change == PrescriptionChange.SaveDraft
                        ? "clinical.prescription_draft_saved"
                        : "clinical.prescription_corrected",
                    prescription.Id, now, reason);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return await GetSavedPrescriptionAsync(prescription.Id, cancellationToken);
            });
        }
        catch (DomainException exception)
        {
            return Result.Failure<PrescriptionModel>(
                ClinicalRecordErrors.Conflict(exception.Message));
        }
        catch (DbUpdateException exception)
        {
            return Result.Failure<PrescriptionModel>(MapWriteFailure(exception));
        }
    }

    private async Task SynchronizeFollowUpAsync(Prescription prescription,
        DateOnly? returnDate, long actorUserId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        FollowUp? followUp = await dbContext.FollowUps.SingleOrDefaultAsync(item =>
            item.PrescriptionId == prescription.Id, cancellationToken);
        if (followUp is null && returnDate.HasValue)
        {
            ClinicalTarget target = await FindTargetAsync(prescription.AppointmentServiceId,
                cancellationToken) ?? throw new InvalidOperationException();
            dbContext.FollowUps.Add(FollowUp.Create(prescription.Id, target.PatientId,
                target.DepartmentId, returnDate.Value, actorUserId, now));
        }
        else if (followUp?.Status == FollowUpStatus.Due && returnDate.HasValue)
            followUp.UpdateDueDate(returnDate.Value);
        else if (followUp?.Status == FollowUpStatus.Due)
            followUp.Cancel(actorUserId, now, "أزيل موعد الإعادة من الروشتة المصححة");
    }

    private async Task<Result<Prescription>> LoadForWriteAsync(long prescriptionId,
        byte[] rowVersion, CancellationToken cancellationToken)
    {
        Prescription? prescription = await PrescriptionForWrite()
            .SingleOrDefaultAsync(item => item.Id == prescriptionId &&
                !item.AppointmentService.Appointment.Patient.IsArchived,
                cancellationToken);
        if (prescription is null)
            return Result.Failure<Prescription>(ClinicalRecordErrors.PrescriptionNotFound);
        return Matches(prescription.RowVersion, rowVersion)
            ? Result.Success(prescription)
            : Result.Failure<Prescription>(ClinicalRecordErrors.ConcurrencyConflict);
    }

    private async Task LockPrescriptionAsync(long patientId, long prescriptionId,
        CancellationToken cancellationToken)
    {
        await TransactionalResourceLock.AcquirePatientAsync(dbContext,
            patientId, cancellationToken);
        await TransactionalResourceLock.AcquirePrescriptionAsync(dbContext,
            prescriptionId, cancellationToken);
    }

    private IQueryable<Prescription> PrescriptionForWrite() => dbContext.Prescriptions
            .Include(item => item.CurrentRevision).ThenInclude(item => item!.Items)
            .Include(item => item.Revisions)
                .ThenInclude(item => item.Items)
            .Include(item => item.AppointmentService).ThenInclude(item => item.Appointment)
                .ThenInclude(item => item.Patient)
            .AsSplitQuery();

    private Task<long?> FindPrescriptionPatientIdAsync(long prescriptionId,
        CancellationToken cancellationToken) => dbContext.Prescriptions.AsNoTracking()
        .Where(item => item.Id == prescriptionId)
        .Select(item => (long?)item.AppointmentService.Appointment.PatientId)
        .SingleOrDefaultAsync(cancellationToken);

    private async Task<Result<PrescriptionModel>> GetSavedPrescriptionAsync(
        long prescriptionId, CancellationToken cancellationToken)
    {
        Result<PrescriptionModel> model = await queryService.GetAsync(prescriptionId,
            includeArchivedPatient: true, cancellationToken);
        return model.IsSuccess
            ? model
            : throw new InvalidOperationException("Saved prescription was not found.");
    }

    private Task<ClinicalTarget?> FindTargetAsync(long appointmentServiceId,
        CancellationToken cancellationToken) => dbContext.AppointmentServices
        .AsNoTracking().Where(item => item.Id == appointmentServiceId)
        .Select(item => new ClinicalTarget(item.Id, item.AppointmentId,
            item.Appointment.PatientId, item.Appointment.DepartmentId,
            item.Appointment.StartAt, item.Appointment.Status, item.Status,
            item.Appointment.Patient.IsArchived))
        .SingleOrDefaultAsync(cancellationToken);

    private static bool IsEligible(ClinicalTarget? target) => target is
    {
        AppointmentStatus: AppointmentStatus.Completed,
        ServiceStatus: AppointmentServiceStatus.Completed,
        PatientArchived: false
    };

    private static Result<PrescriptionContent> ResolveContent(
        PrescriptionContentInput input, DateOnly visitDate)
    {
        try
        {
            DateOnly? returnDate = input.ReturnDate ??
                (input.ReturnAfterDays.HasValue
                    ? visitDate.AddDays(input.ReturnAfterDays.Value) : null);
            if (returnDate <= visitDate)
                return Result.Failure<PrescriptionContent>(
                    ClinicalRecordErrors.Validation(
                        "موعد الإعادة يجب أن يكون بعد تاريخ الزيارة."));
            return Result.Success(new PrescriptionContent(returnDate,
                input.Items.Select(item => (item.MedicineName, item.DoseAmount,
                    item.DoseUnit, item.TimesPerDay, item.FrequencyText,
                    item.DurationText, item.FoodTiming, item.Instructions)).ToArray()));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result.Failure<PrescriptionContent>(
                ClinicalRecordErrors.Validation("موعد الإعادة غير صحيح."));
        }
    }

    private void AddAudit(long actorUserId, string action, long entityId,
        DateTimeOffset occurredAt, string? reason, string entityType = nameof(Prescription)) =>
        dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId, action,
            entityType, entityId.ToString(CultureInfo.InvariantCulture),
            occurredAt, reason));

    private static DateOnly ClinicDate(DateTimeOffset instant) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(instant, AppointmentInfrastructureSupport.ClinicTimeZone)
            .DateTime);

    private static bool Matches(byte[] actual, byte[] expected) =>
        actual.AsSpan().SequenceEqual(expected);

    private static ResultError MapWriteFailure(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
            ? ClinicalRecordErrors.ConcurrencyConflict
            : ClinicalRecordErrors.Conflict("تعذر حفظ السجل بسبب تعارض في البيانات.");

    private enum PrescriptionChange { SaveDraft, Correct }
    private sealed record ClinicalTarget(long AppointmentServiceId,
        long AppointmentId, long PatientId, long DepartmentId,
        DateTimeOffset AppointmentStartAt, AppointmentStatus AppointmentStatus,
        AppointmentServiceStatus ServiceStatus, bool PatientArchived);
}
