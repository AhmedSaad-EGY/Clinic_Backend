using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Cashier;

public interface IPaymentService
{
    Task<Result<IReadOnlyCollection<PaymentMethodModel>>> ListMethodsAsync(
        CancellationToken cancellationToken);
    Task<Result<PostedPaymentModel>> PostAsync(long actorUserId, Guid idempotencyKey,
        PostPaymentInput input, bool adminOverride, CancellationToken cancellationToken);
    Task<Result<PaymentModel>> GetAsync(long actorUserId, long paymentId,
        bool adminOverride, CancellationToken cancellationToken);
    Task<Result<PaymentModel>> GetForPatientAsync(long patientId, long paymentId,
        CancellationToken cancellationToken);
    Task<Result<PaymentPage>> ListForShiftAsync(long actorUserId, long shiftId,
        bool adminOverride, int pageNumber, int pageSize,
        CancellationToken cancellationToken);
    Task<Result<ShiftCollectionSummaryModel>> GetShiftSummaryAsync(long actorUserId,
        long shiftId, bool adminOverride, CancellationToken cancellationToken);
}
