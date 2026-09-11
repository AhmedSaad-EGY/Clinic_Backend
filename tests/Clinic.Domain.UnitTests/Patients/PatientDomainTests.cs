using Clinic.Domain.Common;
using Clinic.Domain.Patients;

namespace Clinic.Domain.UnitTests.Patients;

public sealed class PatientDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 6);

    [Theory]
    [InlineData("+20 101-234-5678", "01012345678")]
    [InlineData("٠١١١٢٣٤٥٦٧٨", "01112345678")]
    [InlineData("00201212345678", "01212345678")]
    public void EgyptianMobileNumberNormalizesSupportedInput(string input, string expected)
    {
        Assert.Equal(expected, EgyptianMobileNumber.Normalize(input, "الهاتف"));
    }

    [Theory]
    [InlineData("01312345678")]
    [InlineData("010123")]
    [InlineData("invalid")]
    public void EgyptianMobileNumberRejectsInvalidInput(string input)
    {
        Assert.Throws<DomainException>(() => EgyptianMobileNumber.Normalize(input, "الهاتف"));
    }

    [Fact]
    public void PatientCreationWithRecordedAgeNormalizesContactData()
    {
        Patient patient = CreatePatient(age: 30, secondaryPhone: "01112345678");

        Assert.Equal("أحمد علي", patient.FullName);
        Assert.Equal("01012345678", patient.PrimaryPhoneNumber);
        Assert.Equal("01112345678", patient.SecondaryPhoneNumber);
        Assert.Equal(30, patient.AgeAtRegistration);
        Assert.Equal(Today, patient.AgeRecordedAt);
        Assert.Null(patient.BirthDate);
    }

    [Fact]
    public void PatientRequiresExactlyOneAgeSource()
    {
        Assert.Throws<DomainException>(() => Patient.Create("مريض", "01012345678", null,
            new DateOnly(1990, 1, 1), 30, PatientGender.Male, null, null, null, null,
            null, 1, Today, Now));
    }

    [Fact]
    public void PatientRejectsSecondaryPhoneEqualToPrimary()
    {
        Assert.Throws<DomainException>(() => CreatePatient(age: 30,
            secondaryPhone: "01012345678"));
    }

    [Fact]
    public void ArchivedPatientCannotBeUpdated()
    {
        Patient patient = CreatePatient(age: 30, secondaryPhone: null);
        patient.Archive(2, Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => patient.Update("اسم جديد", "01012345678", null,
            null, 31, PatientGender.Male, null, null, null, null, null, 2, Today,
            Now.AddMinutes(2)));
    }

    [Fact]
    public void ArchivedPatientCanBeRestoredWithoutChangingClinicalData()
    {
        Patient patient = CreatePatient(age: 30, secondaryPhone: "01112345678");
        patient.Archive(2, Now.AddMinutes(1));

        patient.Restore(3, Now.AddMinutes(2));

        Assert.False(patient.IsArchived);
        Assert.Equal("01112345678", patient.SecondaryPhoneNumber);
        Assert.Equal(3, patient.UpdatedByUserId);
        Assert.Equal(Now.AddMinutes(2), patient.UpdatedAt);
    }

    [Fact]
    public void ActivePatientCannotBeRestored()
    {
        Patient patient = CreatePatient(age: 30, secondaryPhone: null);

        Assert.Throws<DomainException>(() => patient.Restore(2, Now));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public void UnchangedEstimatedAgeKeepsOriginalObservation(int submittedAge)
    {
        Patient patient = CreatePatient(age: 30, secondaryPhone: null);
        DateOnly oneYearLater = Today.AddYears(1);

        patient.Update("أحمد علي", "01012345678", null, null, submittedAge,
            PatientGender.Male, "مدينة نصر", "عنوان جديد", null, null, null, 2,
            oneYearLater, Now.AddYears(1));

        Assert.Equal(30, patient.AgeAtRegistration);
        Assert.Equal(Today, patient.AgeRecordedAt);
    }

    [Fact]
    public void PatientAcceptsBirthDateWhoseCurrentAgeIsExactly130()
    {
        DateOnly birthDate = new(1895, 12, 1);

        Patient patient = Patient.Create("مريض", "01012345678", null, birthDate, null,
            PatientGender.Male, null, null, null, null, null, 1, Today, Now);

        Assert.Equal(birthDate, patient.BirthDate);
    }

    [Fact]
    public void PatientRejectsBirthDateOlderThan130YearsByAge()
    {
        Assert.Throws<DomainException>(() => Patient.Create("مريض", "01012345678", null,
            new DateOnly(1895, 9, 5), null, PatientGender.Male, null, null, null, null,
            null, 1, Today, Now));
    }

    [Fact]
    public void NotesAndTreatmentHistoryAreArchivedWithoutChangingTheirContent()
    {
        PatientNote note = PatientNote.Create(1, "ملاحظة حساسة",
            PatientNoteVisibility.AdminOnly, 2, Now);
        TreatmentHistory history = TreatmentHistory.Create(
            1, Today, "جلسة سابقة", 2, Today, Now);

        note.Archive();
        history.Archive();

        Assert.True(note.IsArchived);
        Assert.Equal("ملاحظة حساسة", note.NoteText);
        Assert.True(history.IsArchived);
        Assert.Equal("جلسة سابقة", history.Description);
    }

    [Fact]
    public void TreatmentHistoryRejectsDefaultOrFutureEventDate()
    {
        Assert.Throws<DomainException>(() => TreatmentHistory.Create(
            1, default, "جلسة", 2, Today, Now));
        Assert.Throws<DomainException>(() => TreatmentHistory.Create(
            1, Today.AddDays(1), "جلسة", 2, Today, Now));
    }

    private static Patient CreatePatient(int age, string? secondaryPhone) => Patient.Create(
        "  أحمد علي  ", "+201012345678", secondaryPhone, null, age, PatientGender.Male,
        "مدينة نصر", "عنوان", null, null, null, 1, Today, Now);
}
