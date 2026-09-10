using System.Reflection;
using Clinic.Domain.Cashier;
using Clinic.Domain.Common;
using Clinic.Domain.Packages;

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

    [Fact]
    public void PaymentAcceptsAppointmentsAndPackagesInOneTransaction()
    {
        PatientPackage patientPackage = CreatePatientPackage(20, 2);

        Payment payment = Payment.Create("PAY-20260907-000002", Guid.NewGuid(),
            new string('b', 64), 1, 2, 3, CollectedAt, null,
            [(1, true, 150m, null)], [(10, 50m)], [(patientPackage, 100m)]);

        Assert.Equal(150m, payment.TotalAmount);
        Assert.Single(payment.AppointmentAllocations);
        Assert.Single(payment.PackageAllocations);
    }

    [Fact]
    public void PaymentRejectsPackageOwnedByAnotherPatient()
    {
        PatientPackage patientPackage = CreatePatientPackage(20, 99);

        Assert.Throws<DomainException>(() => Payment.Create(
            "PAY-20260907-000003", Guid.NewGuid(), new string('c', 64),
            1, 2, 3, CollectedAt, null, [(1, true, 100m, null)], [],
            [(patientPackage, 100m)]));
    }

    private static Payment Create(
        IReadOnlyCollection<(long PaymentMethodId, bool IsCash, decimal Amount,
            string? ReferenceNumber)> methods,
        IReadOnlyCollection<(long AppointmentId, decimal Amount)> appointments) =>
        Payment.Create("PAY-20260907-000001", Guid.NewGuid(), new string('a', 64),
            1, 2, 3, CollectedAt, null, methods, appointments);

    private static PatientPackage CreatePatientPackage(long id, long patientId)
    {
        PatientPackage patientPackage = (PatientPackage)Activator.CreateInstance(
            typeof(PatientPackage), nonPublic: true)!;
        Set(patientPackage, nameof(patientPackage.Id), id);
        Set(patientPackage, nameof(patientPackage.PatientId), patientId);
        Set(patientPackage, nameof(patientPackage.PackageNameSnapshot), "باقة اختبار");
        Set(patientPackage, nameof(patientPackage.NetPriceSnapshot), 100m);
        Set(patientPackage, nameof(patientPackage.PaymentStatus),
            PatientPackagePaymentStatus.Paid);
        return patientPackage;
    }

    private static void Set<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);
}
