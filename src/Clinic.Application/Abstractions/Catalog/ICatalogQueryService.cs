namespace Clinic.Application.Abstractions.Catalog;

public interface ICatalogQueryService
{
    Task<Result<CatalogPage<DepartmentModel>>> ListDepartmentsAsync(
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<CatalogPage<SpecializationModel>>> ListSpecializationsAsync(
        long departmentId,
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<CatalogPage<ServiceModel>>> ListServicesAsync(
        long specializationId,
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<CatalogPage<DeviceModel>>> ListDevicesAsync(
        long departmentId,
        bool includeArchived,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<ServiceModel>> GetServiceAsync(
        long serviceId,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<Result<CatalogPage<ServicePriceHistoryModel>>> ListPriceHistoryAsync(
        long serviceId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}
