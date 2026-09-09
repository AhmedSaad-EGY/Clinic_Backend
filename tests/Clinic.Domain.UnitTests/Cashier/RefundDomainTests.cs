using Clinic.Domain.Cashier;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Cashier;

public sealed class RefundDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RefundAcceptsOriginalMethodsThatEqualTheAppointmentAmount()
    {
        Refund refund = Create([(1, true, 100m, null),
            (2, false, 200m, "REV-1")], 300m);

        Assert.Equal(300m, refund.Amount);
        Assert.Equal(2, refund.MethodAllocations.Count);
        Assert.Single(refund.AppointmentAllocations);
    }

    [Fact]
    public void RefundRejectsMismatchedTotals()
    {
        Assert.Throws<DomainException>(() =>
            Create([(1, true, 100m, null)], 300m));
    }

    [Theory]
    [InlineData(true, "CASH-REF")]
    [InlineData(false, null)]
    public void RefundEnforcesReferenceRules(bool isCash, string? reference)
    {
        Assert.Throws<DomainException>(() =>
            Create([(1, isCash, 300m, reference)], 300m));
    }

    private static Refund Create(
        IReadOnlyCollection<(long OriginalAllocationId, bool IsCash,
            decimal Amount, string? ReferenceNumber)> methods,
        decimal appointmentAmount) => Refund.Create("REF-20260907-000001",
            Guid.NewGuid(), new string('A', 64), 1, 2, 3, 4, Now, null,
            methods, 5, appointmentAmount);
}
