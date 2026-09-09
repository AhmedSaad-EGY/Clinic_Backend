using Clinic.Domain.Catalog;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Catalog;

public sealed class CatalogDomainTests
{
    private static readonly DateTimeOffset Now = new(
        2026,
        9,
        5,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public void DepartmentCreationAlsoCreatesItsSingleActiveRoom()
    {
        Department department = Department.Create(
            "  العلاج الطبيعي  ",
            "  جلسات العلاج  ",
            "  غرفة العلاج  ",
            Now);

        Assert.Equal("العلاج الطبيعي", department.Name);
        Assert.Equal("جلسات العلاج", department.Description);
        Assert.False(department.IsArchived);
        Assert.Equal("غرفة العلاج", department.Room.Name);
        Assert.True(department.Room.IsActive);
    }

    [Fact]
    public void DepartmentArchiveAlsoArchivesItsRoom()
    {
        Department department = Department.Create("الجلدية", null, "غرفة الجلدية", Now);

        department.Archive(Now.AddMinutes(1));

        Assert.True(department.IsArchived);
        Assert.True(department.Room.IsArchived);
        Assert.False(department.Room.IsActive);
    }

    [Fact]
    public void ServicePriceChangeKeepsBothPricePeriods()
    {
        Service service = Service.Create(
            1,
            2,
            "جلسة علاج",
            ServiceType.Session,
            30,
            PricingMode.Fixed,
            300m,
            10,
            Now);

        DateTimeOffset changedAt = Now.AddDays(1);
        service.ChangePrice(350m, 11, changedAt);

        Assert.Equal(350m, service.CurrentUnitPrice);
        Assert.Equal(2, service.PriceHistory.Count);
        Assert.Contains(service.PriceHistory, item =>
            item.UnitPrice == 300m && item.EffectiveTo == changedAt);
        Assert.Contains(service.PriceHistory, item =>
            item.UnitPrice == 350m && item.EffectiveTo is null);
    }

    [Fact]
    public void ServiceRejectsDeviceFromAnotherDepartment()
    {
        Service service = Service.Create(
            1,
            2,
            "ليزر",
            ServiceType.Session,
            20,
            PricingMode.PerUnit,
            5m,
            10,
            Now);
        Device device = Device.Create(2, "جهاز ليزر", "LASER-1");

        Assert.Throws<DomainException>(() => service.ReplaceDeviceAssignments(
            [(device, true)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ServiceRejectsNonPositivePrice(decimal price)
    {
        Assert.Throws<DomainException>(() => Service.Create(
            1,
            2,
            "خدمة",
            ServiceType.Other,
            10,
            PricingMode.Fixed,
            price,
            10,
            Now));
    }
}
