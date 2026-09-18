namespace Clinic.Infrastructure.ClinicalRecords;

public sealed class ClinicalRecordQueryService(ClinicDbContext dbContext,
    TimeProvider timeProvider) : IClinicalRecordQueryService
{
    public async Task<Result<PrescriptionModel>> GetAsync(long prescriptionId,
        bool includeArchivedPatient, CancellationToken cancellationToken)
    {
        PrescriptionModel? model = await TryMapPrescriptionAsync(prescriptionId,
            includeArchivedPatient, cancellationToken);
        return model is null
            ? Result.Failure<PrescriptionModel>(ClinicalRecordErrors.PrescriptionNotFound)
            : Result.Success(model);
    }

    public async Task<Result<ClinicalPage<PrescriptionSummary>>> ListPatientAsync(
        long patientId, bool includeArchivedPatient, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        bool patientExists = await dbContext.Patients.AsNoTracking().AnyAsync(item =>
            item.Id == patientId && (includeArchivedPatient || !item.IsArchived),
            cancellationToken);
        if (!patientExists)
            return Result.Failure<ClinicalPage<PrescriptionSummary>>(
                ClinicalRecordErrors.PatientNotFound);

        IQueryable<Prescription> query = PrescriptionDetails()
            .Where(item => item.AppointmentService.Appointment.PatientId == patientId);
        int count = await query.CountAsync(cancellationToken);
        Prescription[] rows = await query.OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return Result.Success(new ClinicalPage<PrescriptionSummary>(
            rows.Select(MapSummary).ToArray(), pageNumber, pageSize, count));
    }

    public async Task<Result<IReadOnlyCollection<PrescriptionRevisionModel>>>
        ListRevisionsAsync(long prescriptionId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Prescriptions.AnyAsync(item => item.Id == prescriptionId,
            cancellationToken))
            return Result.Failure<IReadOnlyCollection<PrescriptionRevisionModel>>(
                ClinicalRecordErrors.PrescriptionNotFound);
        PrescriptionRevision[] revisions = await dbContext.PrescriptionRevisions
            .AsNoTracking().Include(item => item.Items)
            .Where(item => item.PrescriptionId == prescriptionId)
            .OrderByDescending(item => item.RevisionNumber).ToArrayAsync(cancellationToken);
        Dictionary<long, string> users = await UserNamesAsync(
            revisions.Select(item => item.CreatedByUserId), cancellationToken);
        return Result.Success<IReadOnlyCollection<PrescriptionRevisionModel>>(
            revisions.Select(item => MapRevision(item, users)).ToArray());
    }

    public async Task<Result<PrescriptionRevisionModel>> GetRevisionAsync(
        long prescriptionId, long revisionId, CancellationToken cancellationToken)
    {
        PrescriptionRevision? revision = await dbContext.PrescriptionRevisions
            .AsNoTracking().Include(item => item.Items).SingleOrDefaultAsync(item =>
                item.Id == revisionId && item.PrescriptionId == prescriptionId,
                cancellationToken);
        if (revision is null)
            return Result.Failure<PrescriptionRevisionModel>(
                ClinicalRecordErrors.PrescriptionNotFound);
        Dictionary<long, string> users = await UserNamesAsync(
            [revision.CreatedByUserId], cancellationToken);
        return Result.Success(MapRevision(revision, users));
    }

    public async Task<Result<FollowUpModel>> GetFollowUpAsync(long followUpId,
        bool includeArchivedPatient, CancellationToken cancellationToken)
    {
        FollowUp? followUp = await FollowUpDetails().SingleOrDefaultAsync(item =>
            item.Id == followUpId && (includeArchivedPatient ||
                !item.Prescription.AppointmentService.Appointment.Patient.IsArchived),
            cancellationToken);
        return followUp is null
            ? Result.Failure<FollowUpModel>(ClinicalRecordErrors.FollowUpNotFound)
            : Result.Success(MapFollowUp(followUp));
    }

    public async Task<Result<ClinicalPage<FollowUpModel>>> SearchFollowUpsAsync(
        FollowUpSearch search, CancellationToken cancellationToken)
    {
        IQueryable<FollowUp> query = FollowUpDetails();
        if (!search.IncludeArchivedPatient) query = query.Where(item =>
            !item.Prescription.AppointmentService.Appointment.Patient.IsArchived);
        if (search.PatientId.HasValue) query = query.Where(item =>
            item.PatientId == search.PatientId);
        if (search.DepartmentId.HasValue) query = query.Where(item =>
            item.DepartmentId == search.DepartmentId);
        if (search.DoctorId.HasValue) query = query.Where(item =>
            item.Prescription.AppointmentService.DoctorService.DoctorId == search.DoctorId);
        if (search.Status.HasValue) query = query.Where(item => item.Status == search.Status);
        if (search.FromDate.HasValue) query = query.Where(item =>
            item.ReturnDate >= search.FromDate);
        if (search.ToDate.HasValue) query = query.Where(item =>
            item.ReturnDate <= search.ToDate);
        if (search.OverdueOnly)
        {
            DateOnly today = ClinicDate(timeProvider.GetUtcNow());
            query = query.Where(item => item.Status == FollowUpStatus.Due &&
                item.ReturnDate < today);
        }
        int count = await query.CountAsync(cancellationToken);
        FollowUp[] rows = await query.OrderBy(item => item.ReturnDate)
            .ThenBy(item => item.Id).Skip((search.PageNumber - 1) * search.PageSize)
            .Take(search.PageSize).ToArrayAsync(cancellationToken);
        return Result.Success(new ClinicalPage<FollowUpModel>(
            rows.Select(MapFollowUp).ToArray(), search.PageNumber,
            search.PageSize, count));
    }

    private async Task<PrescriptionModel?> TryMapPrescriptionAsync(long prescriptionId,
        bool includeArchivedPatient, CancellationToken cancellationToken)
    {
        Prescription? prescription = await PrescriptionDetails()
            .SingleOrDefaultAsync(item => item.Id == prescriptionId &&
                (includeArchivedPatient ||
                 !item.AppointmentService.Appointment.Patient.IsArchived),
                cancellationToken);
        if (prescription?.CurrentRevision is null) return null;
        long[] userIds = [prescription.CreatedByUserId,
            prescription.CurrentRevision.CreatedByUserId];
        Dictionary<long, string> users = await UserNamesAsync(userIds,
            cancellationToken);
        Appointment appointment = prescription.AppointmentService.Appointment;
        return new PrescriptionModel(prescription.Id,
            prescription.AppointmentServiceId, appointment.Id, appointment.PatientId,
            appointment.Patient.FileNumber.ToString("D6", CultureInfo.InvariantCulture),
            appointment.Patient.FullName,
            prescription.AppointmentService.DoctorService.DoctorId,
            prescription.AppointmentService.DoctorService.Doctor.Name,
            appointment.DepartmentId, appointment.Room.Department.Name,
            prescription.Status, prescription.CreatedByUserId,
            users[prescription.CreatedByUserId], prescription.CreatedAt,
            prescription.FinalizedAt, prescription.VoidedAt, prescription.VoidReason,
            MapRevision(prescription.CurrentRevision, users),
            Convert.ToBase64String(prescription.RowVersion));
    }

    private IQueryable<Prescription> PrescriptionDetails() => dbContext.Prescriptions
        .AsNoTracking().Include(item => item.CurrentRevision)
            .ThenInclude(item => item!.Items)
        .Include(item => item.AppointmentService).ThenInclude(item => item.Appointment)
            .ThenInclude(item => item.Patient)
        .Include(item => item.AppointmentService).ThenInclude(item => item.Appointment)
            .ThenInclude(item => item.Room).ThenInclude(item => item.Department)
        .Include(item => item.AppointmentService).ThenInclude(item => item.DoctorService)
            .ThenInclude(item => item.Doctor).AsSplitQuery();

    private IQueryable<FollowUp> FollowUpDetails() => dbContext.FollowUps.AsNoTracking()
        .Include(item => item.ResultAppointment)
        .Include(item => item.Prescription).ThenInclude(item => item.CurrentRevision)
        .Include(item => item.Prescription).ThenInclude(item => item.AppointmentService)
            .ThenInclude(item => item.Appointment).ThenInclude(item => item.Patient)
        .Include(item => item.Prescription).ThenInclude(item => item.AppointmentService)
            .ThenInclude(item => item.Appointment).ThenInclude(item => item.Room)
            .ThenInclude(item => item.Department)
        .Include(item => item.Prescription).ThenInclude(item => item.AppointmentService)
            .ThenInclude(item => item.DoctorService).ThenInclude(item => item.Doctor)
        .AsSplitQuery();

    private static PrescriptionSummary MapSummary(Prescription prescription)
    {
        Appointment appointment = prescription.AppointmentService.Appointment;
        PrescriptionRevision revision = prescription.CurrentRevision!;
        return new PrescriptionSummary(prescription.Id,
            prescription.AppointmentServiceId, appointment.Id, appointment.PatientId,
            appointment.Patient.FullName,
            prescription.AppointmentService.DoctorService.DoctorId,
            prescription.AppointmentService.DoctorService.Doctor.Name,
            appointment.Room.Department.Name, prescription.Status,
            prescription.CreatedAt, revision.ReturnDate, revision.RevisionNumber,
            Convert.ToBase64String(prescription.RowVersion));
    }

    private static PrescriptionRevisionModel MapRevision(
        PrescriptionRevision revision, Dictionary<long, string> users) =>
        new(revision.Id, revision.RevisionNumber, revision.CreatedByUserId,
            users[revision.CreatedByUserId], revision.CreatedAt,
            revision.ChangeReason, revision.ReturnDate,
            revision.Items.OrderBy(item => item.SortOrder).Select(item =>
                new PrescriptionItemModel(item.Id, item.SortOrder, item.MedicineName,
                    item.DoseAmount, item.DoseUnit, item.TimesPerDay,
                    item.FrequencyText, item.DurationText, item.FoodTiming,
                    item.Instructions)).ToArray());

    private static FollowUpModel MapFollowUp(FollowUp followUp)
    {
        Prescription prescription = followUp.Prescription;
        Appointment appointment = prescription.AppointmentService.Appointment;
        return new FollowUpModel(followUp.Id, prescription.Id, followUp.PatientId,
            appointment.Patient.FileNumber.ToString("D6", CultureInfo.InvariantCulture),
            appointment.Patient.FullName, followUp.DepartmentId,
            appointment.Room.Department.Name,
            prescription.AppointmentService.DoctorService.DoctorId,
            prescription.AppointmentService.DoctorService.Doctor.Name,
            followUp.ReturnDate, prescription.CurrentRevision?.ReturnDate,
            followUp.Status, followUp.ResultAppointmentId,
            followUp.ResultAppointment?.Status, followUp.CreatedAt,
            followUp.ClosedAt, followUp.ClosureReason,
            Convert.ToBase64String(followUp.RowVersion));
    }

    private async Task<Dictionary<long, string>> UserNamesAsync(
        IEnumerable<long> userIds, CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking().Where(item => userIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.FullName,
                cancellationToken);

    private static DateOnly ClinicDate(DateTimeOffset instant) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(instant, AppointmentInfrastructureSupport.ClinicTimeZone)
            .DateTime);
}
