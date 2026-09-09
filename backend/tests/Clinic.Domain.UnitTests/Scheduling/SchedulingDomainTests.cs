using Clinic.Domain.Catalog;
using Clinic.Domain.Common;
using Clinic.Domain.Scheduling;

namespace Clinic.Domain.UnitTests.Scheduling;

public sealed class SchedulingDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DoctorNormalizesOptionalPhoneAndCanOnlyBeSoftArchived()
    {
        Doctor doctor = Doctor.Create(1, "  د. أحمد  ", " 0100 ", Now);

        doctor.Archive(Now.AddMinutes(1));

        Assert.Equal("د. أحمد", doctor.Name);
        Assert.Equal("0100", doctor.Phone);
        Assert.True(doctor.IsArchived);
        Assert.False(doctor.IsActive);
        Assert.Throws<DomainException>(() => doctor.Update("اسم", null, true, Now));
    }

    [Fact]
    public void DoctorServiceRejectsAServiceFromAnotherDepartment()
    {
        Doctor doctor = Doctor.Create(1, "طبيب", null, Now);
        Service service = Service.Create(2, 3, "خدمة", ServiceType.Session, 30,
            PricingMode.Fixed, 100m, 5, Now);

        Assert.Throws<DomainException>(() => DoctorService.Create(doctor, service));
    }

    [Fact]
    public void ScheduleRejectsInvalidTimeAndDateRanges()
    {
        Assert.Throws<DomainException>(() => DoctorSchedule.Create(
            1, ClinicDayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(11, 0),
            new DateOnly(2026, 9, 5), null));
        Assert.Throws<DomainException>(() => DoctorSchedule.Create(
            1, ClinicDayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0),
            new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 4)));
    }

    [Fact]
    public void FullDayOverrideRequiresBothTimesToBeAbsent()
    {
        DoctorScheduleOverride fullDay = DoctorScheduleOverride.Create(
            1, new DateOnly(2026, 9, 6), null, null,
            DoctorExceptionType.Unavailable, "إجازة", 2, Now);

        Assert.Null(fullDay.StartTime);
        Assert.Throws<DomainException>(() => DoctorScheduleOverride.Create(
            1, new DateOnly(2026, 9, 6), new TimeOnly(10, 0), null,
            DoctorExceptionType.Available, null, 2, Now));
    }

    [Fact]
    public void ClosureIsCancelledWithoutDeletingItsHistory()
    {
        DepartmentClosure closure = DepartmentClosure.Create(
            1, Now, Now.AddHours(2), "صيانة", 2, Now);

        closure.Cancel(2, Now.AddMinutes(5));

        Assert.True(closure.IsCancelled);
        Assert.Equal(2, closure.CancelledByUserId);
        Assert.Throws<DomainException>(() => closure.Cancel(2, Now.AddMinutes(6)));
    }
}
