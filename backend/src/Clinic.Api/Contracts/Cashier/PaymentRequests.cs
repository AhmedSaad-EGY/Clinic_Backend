namespace Clinic.Api.Contracts.Cashier;

public sealed record PaymentMethodAllocationRequest(long PaymentMethodId,
    decimal Amount, string? ReferenceNumber);

public sealed record PostPaymentRequest(IReadOnlyCollection<long> AppointmentIds,
    IReadOnlyCollection<PaymentMethodAllocationRequest> MethodAllocations,
    string? Note = null);

public sealed record AdminPostPaymentRequest(long ShiftId,
    IReadOnlyCollection<long> AppointmentIds,
    IReadOnlyCollection<PaymentMethodAllocationRequest> MethodAllocations,
    string Reason, string? Note = null);
