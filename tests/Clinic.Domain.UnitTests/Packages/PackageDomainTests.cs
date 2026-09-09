using Clinic.Domain.Catalog;
using Clinic.Domain.Common;
using Clinic.Domain.Packages;

namespace Clinic.Domain.UnitTests.Packages;

public sealed class PackageDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 10, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public void CreateBuildsValidActivePackageAndAllowsFreeBasePrice()
    {
        Service service = CreateService(departmentId: 1, PricingMode.Fixed);

        Package package = Package.Create(1, "باقة علاج", 0m, 14, 90,
            [(service, 12, 250m)], 7, Now);

        Assert.True(package.IsActive);
        Assert.Equal(12, package.SessionCount);
        Assert.Equal(0m, package.BasePrice);
        Assert.Single(package.Services);
    }

    [Fact]
    public void CreateRejectsServiceFromAnotherDepartment()
    {
        Service service = CreateService(departmentId: 2, PricingMode.Fixed);

        Assert.Throws<DomainException>(() => Package.Create(1, "باقة", 100m, 14, 90,
            [(service, 2, 50m)], 7, Now));
    }

    [Fact]
    public void CreateRejectsPerUnitService()
    {
        Service service = CreateService(departmentId: 1, PricingMode.PerUnit);

        Assert.Throws<DomainException>(() => Package.Create(1, "باقة", 100m, 14, 90,
            [(service, 2, 50m)], 7, Now));
    }

    [Fact]
    public void CreateRejectsSessionTotalAboveLimit()
    {
        Service service = CreateService(departmentId: 1, PricingMode.Fixed);

        Assert.Throws<DomainException>(() => Package.Create(1, "باقة", 100m, 14, 90,
            [(service, Package.MaximumSessionCount + 1, 50m)], 7, Now));
    }

    [Fact]
    public void CreateRejectsMoneyWithMoreThanTwoDecimalPlaces()
    {
        Service service = CreateService(departmentId: 1, PricingMode.Fixed);

        Assert.Throws<DomainException>(() => Package.Create(1, "باقة", 100.001m, 14, 90,
            [(service, 2, 50m)], 7, Now));
    }

    [Fact]
    public void ArchivedPackageCannotBeChanged()
    {
        Service service = CreateService(departmentId: 1, PricingMode.Fixed);
        Package package = Package.Create(1, "باقة", 100m, 14, 90,
            [(service, 2, 50m)], 7, Now);
        package.Archive(7, Now.AddMinutes(1));

        Assert.Throws<DomainException>(() => package.SetActive(true, 7, Now.AddMinutes(2)));
    }

    private static Service CreateService(long departmentId, PricingMode pricingMode) =>
        Service.Create(departmentId, 1, "خدمة", ServiceType.Session, 30, pricingMode,
            100m, 7, Now);
}
