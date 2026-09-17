namespace Clinic.Application.Abstractions.Discounts;

public interface IDiscountService
{
    Task<Result<DiscountModel>> CreateAsync(long actorUserId, DiscountDefinition definition,
        CancellationToken cancellationToken);
    Task<Result<DiscountModel>> UpdateAsync(long actorUserId, long discountId,
        DiscountDefinition definition, byte[] expectedRowVersion,
        CancellationToken cancellationToken);
    Task<Result<DiscountModel>> SetActiveAsync(long actorUserId, long discountId,
        bool isActive, byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task<Result> ArchiveAsync(long actorUserId, long discountId, byte[] expectedRowVersion,
        CancellationToken cancellationToken);
    Task<Result<DiscountModel>> GetAsync(long discountId,
        CancellationToken cancellationToken);
    Task<Result<DiscountPage>> SearchAsync(DiscountFilter filter,
        CancellationToken cancellationToken);
}
