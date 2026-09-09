using Clinic.Domain.Cashier;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Cashier;

public sealed class PaymentDomainTests
{
    private static readonly DateTimeOffset CollectedAt =
        new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PaymentAcceptsSplitMethodsAndMultipleAppointments()
    {
        Payment payment = Create(
            [(1, true, 100m, null), (2, false, 50m, "VISA-1")],
            [(10, 100m), (11, 50m)]);

        Assert.Equal(150m, payment.TotalAmount);
        Assert.Equal(PaymentRecordStatus.Posted, payment.Status);
        Assert.Equal(2, payment.MethodAllocations.Count);
        Assert.Equal(2, payment.AppointmentAllocations.Count);
    }

    [Fact]
    public void PaymentRejectsMismatchedTotals()
    {
        Assert.Throws<DomainException>(() => Create(
            [(1, true, 100m, null)],
            [(10, 99m)]));
    }

    [Theory]
    [InlineData(true, "CASH-REF")]
    [InlineData(false, null)]
    public void PaymentEnforcesReferenceRules(bool isCash, string? reference)
    {
        Assert.Throws<DomainException>(() => Create(
            [(1, isCash, 100m, reference)],
            [(10, 100m)]));
    }

    [Fact]
    public void PaymentRejectsDuplicateMethodsAndAppointments()
    {
        Assert.Throws<DomainException>(() => Create(
            [(1, true, 50m, null), (1, true, 50m, null)],
            [(10, 100m)]));
        Assert.Throws<DomainException>(() => Create(
            [(1, true, 100m, null)],
            [(10, 50m), (10, 50m)]));
    }

    [Fact]
    public void PaymentTracksAggregateRefundState()
    {
        Payment payment = Create([(1, true, 150m, null)], [(10, 150m)]);

        payment.RecordRefund(50m);
        Assert.Equal(PaymentRecordStatus.PartiallyRefunded, payment.Status);

        payment.RecordRefund(150m);
        Assert.Equal(PaymentRecordStatus.Refunded, payment.Status);
        Assert.Throws<DomainException>(() => payment.RecordRefund(151m));
    }

    private static Payment Create(
        IReadOnlyCollection<(long PaymentMethodId, bool IsCash, decimal Amount,
            string? ReferenceNumber)> methods,
        IReadOnlyCollection<(long AppointmentId, decimal Amount)> appointments) =>
        Payment.Create("PAY-20260907-000001", Guid.NewGuid(), new string('a', 64),
            1, 2, 3, CollectedAt, null, methods, appointments);
}
