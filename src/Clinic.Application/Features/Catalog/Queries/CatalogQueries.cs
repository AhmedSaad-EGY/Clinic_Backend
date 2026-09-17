namespace Clinic.Application.Features.Catalog.Queries;

public sealed record ListDepartmentsQuery(
    bool IncludeArchived,
    int PageNumber,
    int PageSize) : IQuery<CatalogPage<DepartmentModel>>;

public sealed class ListDepartmentsQueryHandler
    : IQueryHandler<ListDepartmentsQuery, CatalogPage<DepartmentModel>>
{
    private readonly ICatalogQueryService _service;

    public ListDepartmentsQueryHandler(ICatalogQueryService service) => _service = service;

    public Task<Result<CatalogPage<DepartmentModel>>> Handle(
        ListDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        Result page = CatalogCommandValidation.ValidatePage(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<CatalogPage<DepartmentModel>>(page.Error))
            : _service.ListDepartmentsAsync(
                query.IncludeArchived,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}

public sealed record ListSpecializationsQuery(
    long DepartmentId,
    bool IncludeArchived,
    int PageNumber,
    int PageSize) : IQuery<CatalogPage<SpecializationModel>>;

public sealed class ListSpecializationsQueryHandler
    : IQueryHandler<ListSpecializationsQuery, CatalogPage<SpecializationModel>>
{
    private readonly ICatalogQueryService _service;

    public ListSpecializationsQueryHandler(ICatalogQueryService service) => _service = service;

    public Task<Result<CatalogPage<SpecializationModel>>> Handle(
        ListSpecializationsQuery query,
        CancellationToken cancellationToken)
    {
        Result page = CatalogCommandValidation.ValidatePage(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<CatalogPage<SpecializationModel>>(page.Error))
            : _service.ListSpecializationsAsync(
                query.DepartmentId,
                query.IncludeArchived,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}

public sealed record ListServicesQuery(
    long SpecializationId,
    bool IncludeArchived,
    int PageNumber,
    int PageSize) : IQuery<CatalogPage<ServiceModel>>;

public sealed class ListServicesQueryHandler
    : IQueryHandler<ListServicesQuery, CatalogPage<ServiceModel>>
{
    private readonly ICatalogQueryService _service;

    public ListServicesQueryHandler(ICatalogQueryService service) => _service = service;

    public Task<Result<CatalogPage<ServiceModel>>> Handle(
        ListServicesQuery query,
        CancellationToken cancellationToken)
    {
        Result page = CatalogCommandValidation.ValidatePage(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<CatalogPage<ServiceModel>>(page.Error))
            : _service.ListServicesAsync(
                query.SpecializationId,
                query.IncludeArchived,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}

public sealed record ListDevicesQuery(
    long DepartmentId,
    bool IncludeArchived,
    int PageNumber,
    int PageSize) : IQuery<CatalogPage<DeviceModel>>;

public sealed class ListDevicesQueryHandler
    : IQueryHandler<ListDevicesQuery, CatalogPage<DeviceModel>>
{
    private readonly ICatalogQueryService _service;

    public ListDevicesQueryHandler(ICatalogQueryService service) => _service = service;

    public Task<Result<CatalogPage<DeviceModel>>> Handle(
        ListDevicesQuery query,
        CancellationToken cancellationToken)
    {
        Result page = CatalogCommandValidation.ValidatePage(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<CatalogPage<DeviceModel>>(page.Error))
            : _service.ListDevicesAsync(
                query.DepartmentId,
                query.IncludeArchived,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}

public sealed record GetServiceQuery(
    long ServiceId,
    bool IncludeArchived) : IQuery<ServiceModel>;

public sealed class GetServiceQueryHandler : IQueryHandler<GetServiceQuery, ServiceModel>
{
    private readonly ICatalogQueryService _service;

    public GetServiceQueryHandler(ICatalogQueryService service) => _service = service;

    public Task<Result<ServiceModel>> Handle(
        GetServiceQuery query,
        CancellationToken cancellationToken) =>
        _service.GetServiceAsync(query.ServiceId, query.IncludeArchived, cancellationToken);
}

public sealed record ListServicePriceHistoryQuery(
    long ServiceId,
    int PageNumber,
    int PageSize) : IQuery<CatalogPage<ServicePriceHistoryModel>>;

public sealed class ListServicePriceHistoryQueryHandler
    : IQueryHandler<ListServicePriceHistoryQuery, CatalogPage<ServicePriceHistoryModel>>
{
    private readonly ICatalogQueryService _service;

    public ListServicePriceHistoryQueryHandler(ICatalogQueryService service) => _service = service;

    public Task<Result<CatalogPage<ServicePriceHistoryModel>>> Handle(
        ListServicePriceHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Result page = CatalogCommandValidation.ValidatePage(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<CatalogPage<ServicePriceHistoryModel>>(page.Error))
            : _service.ListPriceHistoryAsync(
                query.ServiceId,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}
