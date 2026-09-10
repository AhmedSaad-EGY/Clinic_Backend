using Clinic.Domain.Cashier;

namespace Clinic.Application.Abstractions.Cashier;

public sealed record PaymentMethodInput(long PaymentMethodId, decimal Amount,
    string? ReferenceNumber);

public sealed record PostPaymentInput(long? ShiftId,
    IReadOnlyCollection<long> AppointmentIds,
    IReadOnlyCollection<long> PatientPackageIds,
    IReadOnlyCollection<PaymentMethodInput> MethodAllocations,
    string? Note, string? Reason)
{
    public PostPaymentInput(long? shiftId, IReadOnlyCollection<long> appointmentIds,
        IReadOnlyCollection<PaymentMethodInput> methodAllocations, string? note,
        string? reason)
        : this(shiftId, appointmentIds, [], methodAllocations, note, reason)
    {
    }
}

public sealed record PaymentMethodModel(long Id, string Code, string DisplayName,
    bool IsCash, bool RequiresReference, int SortOrder);

public sealed record PaymentMethodAllocationModel(long PaymentMethodId,
    string Code, string DisplayName, bool IsCash, decimal Amount,
    string? ReferenceNumber);

public sealed record AppointmentPaymentAllocationModel(long AppointmentId,
    decimal Amount);

public sealed record PackagePaymentAllocationModel(long PatientPackageId,
    string PackageName, decimal Amount);

public sealed record PaymentModel(long Id, string TransactionNumber, long ShiftId,
    long PatientId, string PatientName, decimal TotalAmount, long CollectedByUserId,
    string CollectedByName, DateTimeOffset CollectedAt, PaymentRecordStatus Status,
    string? Note, IReadOnlyCollection<PaymentMethodAllocationModel> MethodAllocations,
    IReadOnlyCollection<AppointmentPaymentAllocationModel> AppointmentAllocations,
    IReadOnlyCollection<PackagePaymentAllocationModel> PackageAllocations,
    string RowVersion);

public sealed record PostedPaymentModel(PaymentModel Payment, bool WasReplayed);

public sealed record PaymentPage(IReadOnlyCollection<PaymentModel> Items,
    int PageNumber, int PageSize, int TotalCount);

public sealed record PaymentMethodTotalModel(long PaymentMethodId, string Code,
    string DisplayName, bool IsCash, decimal Amount, decimal RefundedAmount,
    decimal NetAmount);

public sealed record ShiftCollectionSummaryModel(long ShiftId, decimal? OpeningBalance,
    decimal CashCollected, decimal CashRefunded, decimal CashNet,
    decimal ElectronicCollected, decimal ElectronicRefunded, decimal ElectronicNet,
    decimal TotalCollected, decimal TotalRefunded, decimal NetTotal,
    decimal? CurrentExpectedCash, decimal? ReconciledExpectedCash,
    bool IsReconciliationStale, bool IsExpectedCashNegative, int PaymentCount,
    int RefundCount,
    IReadOnlyCollection<PaymentMethodTotalModel> MethodTotals);
