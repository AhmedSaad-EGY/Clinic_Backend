using Clinic.Domain.Common;
using Clinic.Domain.Discounts;

namespace Clinic.Domain.UnitTests;

public sealed class DiscountDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 10, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public void PercentageAndFixedDiscountsCalculateOnceAndNeverExceedGross()
    {
        Discount percentage = Create(DiscountType.Percentage, 12.5m);
        Discount fixedAmount = Create(DiscountType.FixedAmount, 300m);

        Assert.Equal(25m, percentage.Calculate(200m));
        Assert.Equal(200m, fixedAmount.Calculate(200m));
    }

    [Fact]
    public void SelectedScopeMustMatchItsApplicationBranch()
    {
        Assert.Throws<DomainException>(() => Discount.Create("خصم", DiscountType.Percentage,
            10m, DiscountAppliesTo.Bookings, DiscountScopeMode.Selected,
            Now, Now.AddDays(1), [], [], [3], 1, Now));
    }

    [Fact]
    public void ArchivedDiscountIsNotEffective()
    {
        Discount discount = Create(DiscountType.Percentage, 10m);

        discount.Archive(1, Now.AddMinutes(1));

        Assert.False(discount.IsEffectiveAt(Now.AddHours(1)));
        Assert.Throws<DomainException>(() => discount.Update("جديد",
            DiscountType.Percentage, 20m, DiscountAppliesTo.Both,
            DiscountScopeMode.All, Now, Now.AddDays(2), [], [], [], 1,
            Now.AddMinutes(2)));
    }

    [Fact]
    public void EffectivePeriodIsStoredAsUtcWithDatabasePrecision()
    {
        DateTimeOffset start = new(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(2));
        start = start.AddMilliseconds(250);

        Discount discount = Discount.Create("خصم", DiscountType.Percentage, 10m,
            DiscountAppliesTo.Bookings, DiscountScopeMode.All, start,
            start.AddMinutes(1), [], [], [], 1, start);

        Assert.Equal(TimeSpan.Zero, discount.StartAt.Offset);
        Assert.Equal(0, discount.StartAt.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(start.ToUnixTimeSeconds()),
            discount.StartAt);
        Assert.Equal(discount.StartAt, discount.CreatedAt);
    }

    [Fact]
    public void EffectivePeriodMustRemainValidAtDatabasePrecision()
    {
        DateTimeOffset start = Now.AddMilliseconds(100);

        Assert.Throws<DomainException>(() => Discount.Create("خصم",
            DiscountType.Percentage, 10m, DiscountAppliesTo.Bookings,
            DiscountScopeMode.All, start, start.AddMilliseconds(200),
            [], [], [], 1, start));
    }

    private static Discount Create(DiscountType type, decimal value) => Discount.Create(
        "خصم اختبار", type, value, DiscountAppliesTo.Both, DiscountScopeMode.All,
        Now.AddHours(-1), Now.AddDays(1), [], [], [], 1, Now);
}
