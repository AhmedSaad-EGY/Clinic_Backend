using System.Reflection;
using Clinic.Domain.Catalog;
using Clinic.Domain.Common;
using Clinic.Domain.Packages;
using Clinic.Domain.Patients;

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
