namespace Clinic.Application.Abstractions.Catalog;

public interface IServiceCatalogService
{
    Task<Result<ServiceModel>> CreateAsync(
        long actorUserId,
        long departmentId,
        long specializationId,
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode,
        decimal unitPrice,
        CancellationToken cancellationToken);

    Task<Result<ServiceModel>> UpdateAsync(
        long actorUserId,
        long serviceId,
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);

    Task<Result<ServiceModel>> ChangePriceAsync(
        long actorUserId,
        long serviceId,
        decimal unitPrice,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);

    Task<Result<ServiceModel>> ReplaceDevicesAsync(
        long actorUserId,
        long serviceId,
        IReadOnlyCollection<ServiceDeviceInput> devices,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);

    Task<Result> ArchiveAsync(
        long actorUserId,
        long serviceId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken);
}
