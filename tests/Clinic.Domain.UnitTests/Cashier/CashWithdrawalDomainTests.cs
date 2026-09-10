using Clinic.Domain.Cashier;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Cashier;

public sealed class CashWithdrawalDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 10, 0, 0,
        TimeSpan.Zero);

    [Fact]
    public void ApprovedRequestCanBeExecutedByItsSecretary()
    {
        CashWithdrawal withdrawal = Create();

        withdrawal.Approve(1, Now.AddMinutes(1), "تمت المراجعة");
        withdrawal.Execute(10, Guid.NewGuid(), Now.AddMinutes(2));

        Assert.Equal(CashWithdrawalStatus.Executed, withdrawal.Status);
        Assert.Equal(1, withdrawal.ReviewedByAdminUserId);
        Assert.Equal(10, withdrawal.ExecutedByUserId);
        Assert.NotNull(withdrawal.ExecutionIdempotencyKey);
    }

    [Fact]
    public void PendingRequestCanBeCancelledOnlyByItsSecretary()
    {
        CashWithdrawal withdrawal = Create();

        Assert.Throws<DomainException>(() => withdrawal.Cancel(11, Now));

        withdrawal.Cancel(10, Now);
        Assert.Equal(CashWithdrawalStatus.Cancelled, withdrawal.Status);
        Assert.Throws<DomainException>(() => withdrawal.Approve(1, Now,
            "موافقة متأخرة"));
    }

    [Fact]
    public void RejectedRequestCannotBeExecuted()
    {
        CashWithdrawal withdrawal = Create();
        withdrawal.Reject(1, Now, "غير مبرر");

        Assert.Throws<DomainException>(() => withdrawal.Execute(10,
            Guid.NewGuid(), Now));
    }

    [Fact]
    public void AdminCanRejectApprovedRequestBeforeExecution()
    {
        CashWithdrawal withdrawal = Create();
        withdrawal.Approve(1, Now.AddMinutes(1), "موافقة أولية");

        withdrawal.Reject(1, Now.AddMinutes(2), "لم يعد السحب مطلوبًا");

        Assert.Equal(CashWithdrawalStatus.Rejected, withdrawal.Status);
        Assert.Equal("لم يعد السحب مطلوبًا", withdrawal.DecisionReason);
        Assert.Throws<DomainException>(() => withdrawal.Execute(10,
            Guid.NewGuid(), Now.AddMinutes(3)));
    }

    [Fact]
    public void AmountMustBePositiveAndHaveAtMostTwoDecimals()
    {
        Assert.Throws<DomainException>(() => CashWithdrawal.Create(
            "WDL-20260910-000001", Guid.NewGuid(), new string('A', 64),
            20, 0, "مصروف", 10, Now));
        Assert.Throws<DomainException>(() => CashWithdrawal.Create(
            "WDL-20260910-000001", Guid.NewGuid(), new string('A', 64),
            20, 1.001m, "مصروف", 10, Now));
        Assert.Throws<DomainException>(() => CashWithdrawal.Create(
            "WDL-20260910-000001", Guid.NewGuid(), new string('A', 64),
            20, CashWithdrawal.MaximumAmount + 0.01m, "مصروف", 10, Now));
    }

    private static CashWithdrawal Create() => CashWithdrawal.Create(
        "WDL-20260910-000001", Guid.NewGuid(), new string('A', 64),
        20, 50, "شراء مستلزمات", 10, Now);
}
