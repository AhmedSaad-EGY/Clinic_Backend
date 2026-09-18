namespace Clinic.Infrastructure.Patients;

internal static class PatientInfrastructureSupport
{
    private static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();

    public static bool MatchesVersion(byte[] actual, byte[] expected) =>
        actual.AsSpan().SequenceEqual(expected);

    public static void AddAudit(ClinicDbContext dbContext, long actorUserId, string action,
        string entityType, long entityId, DateTimeOffset occurredAt, string? reason = null)
    {
        dbContext.AuditLogs.Add(AuditLog.CreateForUser(actorUserId, action, entityType,
            entityId.ToString(CultureInfo.InvariantCulture), occurredAt, reason));
    }

    public static ResultError MapDatabaseFailure(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return PatientErrors.ConcurrencyConflict;
        }

        if (exception.InnerException is SqlException sqlException &&
            sqlException.Number is 2601 or 2627 &&
            (sqlException.Message.Contains("UX_Patients_PrimaryPhoneNumber",
                 StringComparison.Ordinal) ||
             sqlException.Message.Contains("UX_Patients_SecondaryPhoneNumber",
                 StringComparison.Ordinal)))
        {
            return PatientErrors.DuplicatePhoneConflict;
        }

        throw new InvalidOperationException("Patient persistence failed.", exception);
    }

    public static DateOnly ClinicDate(DateTimeOffset instant) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(instant, ClinicTimeZone).DateTime);

    public static DateTimeOffset ClinicDayStartUtc(DateOnly date)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (ClinicTimeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, ClinicTimeZone));
    }

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

internal static class PatientMapper
{
    public static PatientDetails Details(Patient patient, DateOnly today) => new(
        patient.Id, FormatFileNumber(patient.FileNumber), patient.FullName,
        patient.PrimaryPhoneNumber, patient.SecondaryPhoneNumber, patient.BirthDate,
        patient.AgeAtRegistration, patient.AgeRecordedAt, patient.GetCurrentAge(today),
        patient.Gender, patient.Area, patient.Address, patient.Email, patient.GuardianName,
        patient.GuardianPhoneNumber, patient.IsArchived, patient.CreatedAt,
        patient.UpdatedAt, Convert.ToBase64String(patient.RowVersion));

    public static PatientSummary Summary(Patient patient, DateOnly today) => new(
        patient.Id, FormatFileNumber(patient.FileNumber), patient.FullName,
        patient.PrimaryPhoneNumber, patient.SecondaryPhoneNumber, patient.GetCurrentAge(today),
        patient.Gender, patient.Area, patient.IsArchived,
        Convert.ToBase64String(patient.RowVersion));

    public static PatientNoteModel Note(PatientNote note) => new(note.Id, note.PatientId,
        note.NoteText, note.Visibility, note.CreatedByUserId, note.CreatedAt,
        note.IsArchived, Convert.ToBase64String(note.RowVersion));

    public static TreatmentHistoryModel Treatment(TreatmentHistory item) => new(item.Id,
        item.PatientId, item.EventDate, item.Description, item.CreatedByUserId, item.CreatedAt,
        item.IsArchived, Convert.ToBase64String(item.RowVersion));

    internal static string FormatFileNumber(long fileNumber) =>
        fileNumber.ToString("D6", CultureInfo.InvariantCulture);

}

internal static class PatientAuditActions
{
    public const string PatientCreated = "patients.patient.created";
    public const string PatientUpdated = "patients.patient.updated";
    public const string PatientArchived = "patients.patient.archived";
    public const string PatientRestored = "patients.patient.restored";
    public const string NoteCreated = "patients.note.created";
    public const string NoteArchived = "patients.note.archived";
    public const string TreatmentHistoryCreated = "patients.treatment_history.created";
    public const string TreatmentHistoryArchived = "patients.treatment_history.archived";
}
