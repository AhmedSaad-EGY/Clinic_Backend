using Clinic.Domain.Cashier;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Cashier;

public sealed class ShiftDomainTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShiftLifecycleRecordsOpeningReconciliationAndClosure()
    {
        Shift shift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);

        Assert.Equal(ShiftStatus.Scheduled, shift.GetEffectiveStatus(Start.AddMinutes(-1)));
        Assert.Equal(ShiftStatus.Open, shift.GetEffectiveStatus(Start));
        Assert.Equal(ShiftStatus.Grace,
            shift.GetEffectiveStatus(Start.AddHours(4)));

        shift.RecordOpeningBalance(100, 3, Start);
        Assert.True(shift.CanCollect(Start.AddHours(4).AddMinutes(9)));
        Assert.False(shift.CanCollect(Start.AddHours(4).AddMinutes(10)));

        shift.Reconcile(100, 95, 3, Start.AddHours(4));
        Assert.Throws<DomainException>(() => shift.Close(
            100, 3, Start.AddHours(4), allowEarlyClose: false, note: null));
        shift.Close(100, 3, Start.AddHours(4), allowEarlyClose: false,
            note: "عجز خمسة جنيهات");

        Assert.Equal(ShiftStatus.Closed, shift.Status);
        Assert.Equal(-5, shift.CashVariance);
        Assert.Equal(3, shift.ClosedByUserId);
    }

    [Fact]
    public void OpeningBalanceIsRequiredAndCanBeZero()
    {
        Shift shift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);

        Assert.Throws<DomainException>(() => shift.Reconcile(
            0, 0, 3, Start.AddHours(4)));

        shift.RecordOpeningBalance(0, 3, Start);

        Assert.Equal(0, shift.OpeningBalance);
        Assert.True(shift.CanCollect(Start));
    }

    [Fact]
    public void SecretaryCannotCloseEarlyButAdminCan()
    {
        Shift secretaryShift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);
        secretaryShift.RecordOpeningBalance(50, 3, Start);
        secretaryShift.Reconcile(50, 50, 3, Start.AddHours(2));

        Assert.Throws<DomainException>(() => secretaryShift.Close(
            50, 3, Start.AddHours(2), allowEarlyClose: false, note: null));
        secretaryShift.Close(50, 3, Start.AddHours(4),
            allowEarlyClose: false, note: null);
        Assert.Equal(ShiftStatus.Closed, secretaryShift.Status);

        Shift adminShift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);
        adminShift.RecordOpeningBalance(50, 1, Start);
        adminShift.Reconcile(50, 50, 1, Start.AddHours(2));
        adminShift.Close(50, 1, Start.AddHours(2), allowEarlyClose: true,
            "إغلاق إداري");

        Assert.Equal(ShiftStatus.Closed, adminShift.Status);
    }

    [Fact]
    public void ExtendingShiftClearsPreviousReconciliation()
    {
        Shift shift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);
        shift.RecordOpeningBalance(100, 3, Start);
        shift.Reconcile(100, 100, 3, Start.AddHours(4));

        shift.Extend(Start.AddHours(5), 1, Start.AddHours(4));

        Assert.Equal(Start.AddHours(5).AddMinutes(10), shift.GraceEndsAt);
        Assert.Null(shift.ExpectedCash);
        Assert.Null(shift.DeclaredCash);
        Assert.Null(shift.CashVariance);
        Assert.Equal(ShiftStatus.Open, shift.Status);
        Assert.Equal(Start, shift.ActualOpenedAt);
    }

    [Fact]
    public void FutureShiftCanOnlyBeCancelledOnce()
    {
        Shift shift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);

        shift.Cancel(1, Start.AddHours(-1), "إجازة الموظفة");

        Assert.Equal(ShiftStatus.Cancelled, shift.Status);
        Assert.Equal("إجازة الموظفة", shift.CancellationReason);
        Assert.Throws<DomainException>(() => shift.Cancel(
            1, Start.AddHours(-1), "إلغاء ثانٍ"));
    }

    [Fact]
    public void InvalidScheduleAndNegativeMoneyAreRejected()
    {
        Assert.Throws<DomainException>(() => Shift.Create(
            2, Start, Start, 10, 1));
        Assert.Throws<DomainException>(() => Shift.Create(
            2, Start, Start.AddHours(1), 121, 1));

        Shift shift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);
        Assert.Throws<DomainException>(() => shift.RecordOpeningBalance(
            -1, 3, Start));
    }

    [Fact]
    public void CollectionInvalidatesPreviousReconciliation()
    {
        Shift shift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);
        shift.RecordOpeningBalance(100, 3, Start);
        shift.Reconcile(100, 100, 3, Start.AddHours(2));

        shift.RegisterCollection(Start.AddHours(2));

        Assert.Null(shift.ExpectedCash);
        Assert.Null(shift.DeclaredCash);
        Assert.Null(shift.CashVariance);
    }

    [Fact]
    public void RefundAllowsNegativeExpectedCashAndInvalidatesReconciliation()
    {
        Shift shift = Shift.Create(2, Start, Start.AddHours(4), 10, 1);
        shift.RecordOpeningBalance(0, 3, Start);
        shift.Reconcile(0, 0, 3, Start.AddHours(2));

        shift.RegisterRefund(Start.AddHours(2));
        shift.Reconcile(-100, 0, 3, Start.AddHours(4));
        shift.Close(-100, 3, Start.AddHours(4), allowEarlyClose: false,
            "تم رد مبلغ من درج دون تحصيل نقدي سابق في الشيفت");

        Assert.Equal(100, shift.CashVariance);
    }
}
