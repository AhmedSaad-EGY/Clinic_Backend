namespace Clinic.Domain.Patients;

public sealed class Patient : AggregateRoot
{
    private Patient()
    {
    }

    private Patient(
        string fullName,
        string primaryPhoneNumber,
        string? secondaryPhoneNumber,
        DateOnly? birthDate,
        int? age,
        PatientGender gender,
        string? area,
        string? address,
        string? email,
        string? guardianName,
        string? guardianPhoneNumber,
        long createdByUserId,
        DateOnly recordedOn,
        DateTimeOffset createdAt)
    {
        ApplyDetails(fullName, primaryPhoneNumber, secondaryPhoneNumber, birthDate, age,
            gender, area, address, email, guardianName, guardianPhoneNumber, recordedOn,
            preserveExistingAgeObservation: false);
        PatientGuard.PositiveId(createdByUserId, "المستخدم المنشئ");
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public long FileNumber { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string PrimaryPhoneNumber { get; private set; } = string.Empty;
    public string? SecondaryPhoneNumber { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public int? AgeAtRegistration { get; private set; }
    public DateOnly? AgeRecordedAt { get; private set; }
    public PatientGender Gender { get; private set; }
    public string? Area { get; private set; }
    public string? Address { get; private set; }
    public string? Email { get; private set; }
    public string? GuardianName { get; private set; }
    public string? GuardianPhoneNumber { get; private set; }
    public bool IsArchived { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long? UpdatedByUserId { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Patient Create(
        string fullName, string primaryPhoneNumber, string? secondaryPhoneNumber,
        DateOnly? birthDate, int? age, PatientGender gender, string? area,
        string? address, string? email, string? guardianName,
        string? guardianPhoneNumber, long createdByUserId, DateOnly recordedOn,
        DateTimeOffset createdAt) =>
        new(fullName, primaryPhoneNumber, secondaryPhoneNumber, birthDate, age, gender,
            area, address, email, guardianName, guardianPhoneNumber, createdByUserId,
            recordedOn, createdAt);

    public void Update(
        string fullName, string primaryPhoneNumber, string? secondaryPhoneNumber,
        DateOnly? birthDate, int? age, PatientGender gender, string? area,
        string? address, string? email, string? guardianName,
        string? guardianPhoneNumber, long updatedByUserId, DateOnly recordedOn,
        DateTimeOffset updatedAt)
    {
        EnsureActive();
        ApplyDetails(fullName, primaryPhoneNumber, secondaryPhoneNumber, birthDate, age,
            gender, area, address, email, guardianName, guardianPhoneNumber, recordedOn,
            preserveExistingAgeObservation: true);
        PatientGuard.PositiveId(updatedByUserId, "المستخدم المعدل");
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = updatedAt;
    }

    public void Archive(long archivedByUserId, DateTimeOffset archivedAt)
    {
        EnsureActive();
        PatientGuard.PositiveId(archivedByUserId, "المستخدم المؤرشف");
        IsArchived = true;
        UpdatedByUserId = archivedByUserId;
        UpdatedAt = archivedAt;
    }

    public void Restore(long restoredByAdminUserId, DateTimeOffset restoredAt)
    {
        if (!IsArchived)
        {
            throw new DomainException("ملف المريض غير مؤرشف.");
        }

        PatientGuard.PositiveId(restoredByAdminUserId, "الأدمن");
        IsArchived = false;
        UpdatedByUserId = restoredByAdminUserId;
        UpdatedAt = restoredAt;
    }

    public int GetCurrentAge(DateOnly onDate)
    {
        if (BirthDate is DateOnly birthDate)
        {
            return CalculateAge(birthDate, onDate);
        }

        DateOnly recordedAt = AgeRecordedAt ?? onDate;
        int elapsedYears = onDate.Year - recordedAt.Year;
        if (recordedAt.AddYears(elapsedYears) > onDate)
        {
            elapsedYears--;
        }

        return (AgeAtRegistration ?? 0) + Math.Max(0, elapsedYears);
    }

    private void ApplyDetails(
        string fullName, string primaryPhoneNumber, string? secondaryPhoneNumber,
        DateOnly? birthDate, int? age, PatientGender gender, string? area,
        string? address, string? email, string? guardianName,
        string? guardianPhoneNumber, DateOnly recordedOn,
        bool preserveExistingAgeObservation)
    {
        FullName = PatientGuard.RequiredText(fullName, "اسم المريض", 200);
        PrimaryPhoneNumber = EgyptianMobileNumber.Normalize(primaryPhoneNumber, "رقم الموبايل الأساسي");
        SecondaryPhoneNumber = string.IsNullOrWhiteSpace(secondaryPhoneNumber)
            ? null
            : EgyptianMobileNumber.Normalize(secondaryPhoneNumber, "رقم الموبايل الإضافي");
        if (SecondaryPhoneNumber == PrimaryPhoneNumber)
        {
            throw new DomainException("رقم الموبايل الإضافي يجب أن يختلف عن الرقم الأساسي.");
        }

        if ((birthDate is null) == (age is null))
        {
            throw new DomainException("يجب إدخال تاريخ الميلاد أو العمر، وليس كليهما.");
        }

        if (birthDate is not null)
        {
            int currentAge = CalculateAge(birthDate.Value, recordedOn);
            if (birthDate > recordedOn || currentAge > 130)
            {
                throw new DomainException("تاريخ الميلاد غير صحيح.");
            }

            BirthDate = birthDate;
            AgeAtRegistration = null;
            AgeRecordedAt = null;
        }
        else
        {
            if (age is < 0 or > 130)
            {
                throw new DomainException("العمر يجب أن يكون بين 0 و130 سنة.");
            }

            bool unchangedEstimate = preserveExistingAgeObservation && BirthDate is null &&
                AgeAtRegistration.HasValue && AgeRecordedAt.HasValue &&
                (age == AgeAtRegistration || age == GetCurrentAge(recordedOn));
            if (!unchangedEstimate)
            {
                BirthDate = null;
                AgeAtRegistration = age;
                AgeRecordedAt = recordedOn;
            }
        }

        if (!Enum.IsDefined(gender))
        {
            throw new DomainException("نوع المريض غير صحيح.");
        }

        Gender = gender;
        Area = PatientGuard.OptionalText(area, "المنطقة", 150);
        Address = PatientGuard.OptionalText(address, "العنوان", 500);
        Email = PatientGuard.OptionalText(email, "البريد الإلكتروني", 256);
        GuardianName = PatientGuard.OptionalText(guardianName, "اسم ولي الأمر", 200);
        GuardianPhoneNumber = string.IsNullOrWhiteSpace(guardianPhoneNumber)
            ? null
            : EgyptianMobileNumber.Normalize(guardianPhoneNumber, "رقم موبايل ولي الأمر");
    }

    private static int CalculateAge(DateOnly birthDate, DateOnly onDate)
    {
        int age = onDate.Year - birthDate.Year;
        return birthDate.AddYears(age) > onDate ? age - 1 : age;
    }

    private void EnsureActive()
    {
        if (IsArchived)
        {
            throw new DomainException("لا يمكن تعديل ملف مريض مؤرشف.");
        }
    }
}
