using System.Reflection;
using Clinic.Domain.Catalog;
using Clinic.Domain.Common;
using Clinic.Domain.Appointments;
using Clinic.Domain.Packages;
using Clinic.Domain.Patients;
using Clinic.Domain.Discounts;

namespace Clinic.Domain.UnitTests.Packages;

public sealed class PatientPackageDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 10, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public void FreePackageStartsActivationWindowAndCreatesEverySession()
    {
        (Patient patient, Package package) = CreateGraph(0m, 3);

        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('A', 64), 7, Now);

        Assert.Equal(PatientPackagePaymentStatus.NotRequired, purchase.PaymentStatus);
        Assert.Equal(Now, purchase.ActivationWindowStartedAt);
        Assert.Equal(Now.AddDays(14), purchase.ActivationDeadlineAt);
        Assert.Equal(3, purchase.Services.Single().Sessions.Count);
        Assert.All(purchase.Services.Single().Sessions,
            session => Assert.Equal(PackageSessionStatus.Available, session.Status));
    }

    [Fact]
    public void PaidPackageStartsUnpaidWithoutUsageDates()
    {
        (Patient patient, Package package) = CreateGraph(500m, 2);

        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('B', 64), 7, Now);

        Assert.Equal(PatientPackagePaymentStatus.Unpaid, purchase.PaymentStatus);
        Assert.Null(purchase.ActivationWindowStartedAt);
        Assert.Null(purchase.ActivationDeadlineAt);
        Assert.Null(purchase.FirstUsedAt);
        Assert.Null(purchase.ExpiresAt);
    }

    [Fact]
    public void DiscountIsSnapshottedAndCanMakePackageFree()
    {
        (Patient patient, Package package) = CreateGraph(500m, 2);
        Discount discount = Discount.Create("خصم كامل", DiscountType.Percentage, 100m,
            DiscountAppliesTo.Packages, DiscountScopeMode.All, Now.AddDays(-1),
            Now.AddDays(1), [], [], [], 7, Now);
        Set(discount, nameof(discount.Id), 8L);

        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('Z', 64), 7, Now, discount);

        Assert.Equal(8, purchase.DiscountId);
        Assert.Equal(500m, purchase.DiscountAmountSnapshot);
        Assert.Equal(0m, purchase.NetPriceSnapshot);
        Assert.Equal(PatientPackagePaymentStatus.NotRequired, purchase.PaymentStatus);
    }

    [Fact]
    public void FullPaymentStartsActivationWindowExactlyOnce()
    {
        (Patient patient, Package package) = CreateGraph(500m, 2);
        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('P', 64), 7, Now);

        purchase.RecordFullPayment(500m, Now.AddHours(1));

        Assert.Equal(PatientPackagePaymentStatus.Paid, purchase.PaymentStatus);
        Assert.Equal(Now.AddHours(1), purchase.ActivationWindowStartedAt);
        Assert.Equal(Now.AddHours(1).AddDays(14), purchase.ActivationDeadlineAt);
        Assert.Throws<DomainException>(() =>
            purchase.RecordFullPayment(500m, Now.AddHours(2)));
    }

    [Fact]
    public void FullPaymentRequiresExactSnapshotPrice()
    {
        (Patient patient, Package package) = CreateGraph(500m, 2);
        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('Q', 64), 7, Now);

        Assert.Throws<DomainException>(() => purchase.RecordFullPayment(499m, Now));
        Assert.Equal(PatientPackagePaymentStatus.Unpaid, purchase.PaymentStatus);
        Assert.Null(purchase.ActivationWindowStartedAt);
    }

    [Fact]
    public void SnapshotDoesNotChangeWhenCatalogDefinitionChanges()
    {
        (Patient patient, Package package) = CreateGraph(500m, 2);
        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('C', 64), 7, Now);
        Service service = package.Services.Single().Service;

        service.UpdateDetails("اسم جديد", ServiceType.Session, 45, PricingMode.Fixed);
        package.Update("باقة جديدة", 600m, 20, 120, [(service, 4, 175m)], 7,
            Now.AddMinutes(1));

        Assert.Equal("باقة اختبار", purchase.PackageNameSnapshot);
        Assert.Equal("جلسة اختبار", purchase.Services.Single().ServiceNameSnapshot);
        Assert.Equal(2, purchase.TotalSessions);
        Assert.Equal(150m, purchase.Services.Single().UnitPriceSnapshot);
    }

    [Fact]
    public void ExtensionRequiresAnExistingDeadlineAndALaterValue()
    {
        (Patient patient, Package package) = CreateGraph(0m, 1);
        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('D', 64), 7, Now);

        Assert.Throws<DomainException>(() => purchase.Extend(
            PatientPackageExtensionType.ActivationDeadline, Now.AddDays(10), 7,
            Now.AddMinutes(1)));
        purchase.Extend(PatientPackageExtensionType.ActivationDeadline, Now.AddDays(20), 7,
            Now.AddMinutes(2));
        Assert.Equal(Now.AddDays(20), purchase.ActivationDeadlineAt);
        Assert.Throws<DomainException>(() => purchase.Extend(
            PatientPackageExtensionType.UsageExpiry, Now.AddDays(100), 7,
            Now.AddMinutes(3)));
    }

    [Fact]
    public void RegistrationRejectsArchivedPatient()
    {
        (Patient patient, Package package) = CreateGraph(500m, 1);
        patient.Archive(7, Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => PatientPackage.Register(patient, package,
            Guid.NewGuid(), new string('E', 64), 7, Now.AddMinutes(2)));
    }

    [Fact]
    public void RegistrationRejectsDefinitionWithoutTimePolicies()
    {
        (Patient patient, Package package) = CreateGraph(500m, 1);
        Set<int?>(package, nameof(package.ActivationGraceDays), null);

        Assert.Throws<DomainException>(() => PatientPackage.Register(patient, package,
            Guid.NewGuid(), new string('F', 64), 7, Now));
    }

    [Fact]
    public void PackageRejectsDurationsThatCannotBeUsedSafely()
    {
        (Patient _, Package package) = CreateGraph(500m, 1);
        Service service = package.Services.Single().Service;

        DomainException exception = Assert.Throws<DomainException>(() => package.Update(
            "باقة اختبار", 500m, int.MaxValue, 90, [(service, 1, 150m)], 7,
            Now.AddMinutes(1)));

        Assert.Contains("36500", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionCanBeReservedReleasedAndConsumedWithHistoricalLinks()
    {
        (Patient patient, Package package) = CreateGraph(0m, 1);
        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('S', 64), 7, Now);
        Set(purchase, nameof(purchase.Id), 8L);
        PatientPackageService purchasedService = purchase.Services.Single();
        Set(purchasedService, nameof(purchasedService.Id), 9L);
        Set(purchasedService, nameof(purchasedService.PatientPackageId), 8L);
        PackageSession session = purchasedService.Sessions.Single();
        Set(session, nameof(session.Id), 10L);
        Set(session, nameof(session.PatientPackageId), 8L);
        Appointment appointment = Appointment.Create(patient.Id, 11, package.DepartmentId,
            Now.AddHours(1), false, 7, Now);
        AppointmentService line = appointment.AddService(3, 12, 30, 1, 150m, []);
        Set(appointment, nameof(appointment.Id), 13L);
        Set(line, nameof(line.Id), 14L);
        Set(line, nameof(line.AppointmentId), 13L);
        appointment.CoverByPackage(purchase, new Dictionary<long, decimal> { [3] = 150m },
            Guid.NewGuid(), new string('R', 64));

        PackageSessionBooking first = session.Reserve(appointment, line, Now);
        first.Release(Now.AddMinutes(1));
        PackageSessionBooking second = session.Reserve(appointment, line, Now.AddMinutes(2));
        second.Consume(Now.AddMinutes(3));

        Assert.Equal(PackageSessionBookingStatus.Released, first.Status);
        Assert.Equal(PackageSessionBookingStatus.Consumed, second.Status);
        Assert.Equal(PackageSessionStatus.Consumed, session.Status);
    }

    [Fact]
    public void FirstUseStartsAtVisitTimeAndActivationDeadlineIsExclusive()
    {
        (Patient patient, Package package) = CreateGraph(0m, 1);
        PatientPackage purchase = PatientPackage.Register(patient, package, Guid.NewGuid(),
            new string('U', 64), 7, Now);

        Assert.Throws<DomainException>(() =>
            purchase.RecordFirstUse(Now.AddDays(14)));
        purchase.RecordFirstUse(Now.AddDays(2));

        Assert.Equal(Now.AddDays(2), purchase.FirstUsedAt);
        Assert.Equal(Now.AddDays(92), purchase.ExpiresAt);
    }

    private static (Patient Patient, Package Package) CreateGraph(decimal basePrice,
        int sessions)
    {
        Department department = Department.Create("قسم اختبار", null, "غرفة", Now);
        Set(department, nameof(department.Id), 1L);
        Specialization specialization = Specialization.Create(1, "تخصص اختبار");
        Set(specialization, nameof(specialization.Id), 2L);
        Service service = Service.Create(1, 2, "جلسة اختبار", ServiceType.Session, 30,
            PricingMode.Fixed, 200m, 7, Now);
        Set(service, nameof(service.Id), 3L);
        Set(service, nameof(service.Specialization), specialization);
        Patient patient = Patient.Create("مريض اختبار", "01012345678", null, null, 30,
            PatientGender.Male, null, null, null, null, null, 7,
            DateOnly.FromDateTime(Now.UtcDateTime), Now);
        Set(patient, nameof(patient.Id), 4L);
        Package package = Package.Create(1, "باقة اختبار", basePrice, 14, 90,
            [(service, sessions, 150m)], 7, Now);
        Set(package, nameof(package.Id), 5L);
        Set(package, nameof(package.Department), department);
        Set(package.Services.Single(), nameof(PackageService.Id), 6L);
        return (patient, package);
    }

    private static void Set<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);
}
