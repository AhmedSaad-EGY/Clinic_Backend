using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Packages;

public sealed record SearchAdminPackagesQuery(AdminPackageFilter Filter) : IQuery<PackagePage>;
public sealed record GetAdminPackageQuery(long PackageId) : IQuery<PackageModel>;
public sealed record SearchAvailablePackagesQuery(PackageCatalogFilter Filter) : IQuery<PackagePage>;
public sealed record GetAvailablePackageQuery(long PackageId) : IQuery<PackageModel>;

public sealed class SearchAdminPackagesQueryHandler
    : IQueryHandler<SearchAdminPackagesQuery, PackagePage>
{
    private readonly IPackageQueryService _service;
    public SearchAdminPackagesQueryHandler(IPackageQueryService service) => _service = service;

    public Task<Result<PackagePage>> Handle(SearchAdminPackagesQuery query,
        CancellationToken cancellationToken) => ValidatePage(query.Filter.PageNumber,
        query.Filter.PageSize) is { IsFailure: true } failure
        ? Task.FromResult(Result.Failure<PackagePage>(failure.Error))
        : _service.SearchAdminAsync(query.Filter, cancellationToken);

    internal static Result ValidatePage(int pageNumber, int pageSize) =>
        pageNumber >= 1 && pageSize is >= 1 and <= 100
            ? Result.Success()
            : Result.Failure(PackageErrors.Validation(
                "رقم الصفحة يجب أن يبدأ من 1 وحجم الصفحة يجب أن يكون بين 1 و100."));
}

public sealed class GetAdminPackageQueryHandler : IQueryHandler<GetAdminPackageQuery, PackageModel>
{
    private readonly IPackageQueryService _service;
    public GetAdminPackageQueryHandler(IPackageQueryService service) => _service = service;
    public Task<Result<PackageModel>> Handle(GetAdminPackageQuery query,
        CancellationToken cancellationToken) => _service.GetAdminAsync(query.PackageId,
        cancellationToken);
}

public sealed class SearchAvailablePackagesQueryHandler
    : IQueryHandler<SearchAvailablePackagesQuery, PackagePage>
{
    private readonly IPackageQueryService _service;
    public SearchAvailablePackagesQueryHandler(IPackageQueryService service) => _service = service;
    public Task<Result<PackagePage>> Handle(SearchAvailablePackagesQuery query,
        CancellationToken cancellationToken)
    {
        Result validation = SearchAdminPackagesQueryHandler.ValidatePage(query.Filter.PageNumber,
            query.Filter.PageSize);
        return validation.IsFailure
            ? Task.FromResult(Result.Failure<PackagePage>(validation.Error))
            : _service.SearchAvailableAsync(query.Filter, cancellationToken);
    }
}

public sealed class GetAvailablePackageQueryHandler
    : IQueryHandler<GetAvailablePackageQuery, PackageModel>
{
    private readonly IPackageQueryService _service;
    public GetAvailablePackageQueryHandler(IPackageQueryService service) => _service = service;
    public Task<Result<PackageModel>> Handle(GetAvailablePackageQuery query,
        CancellationToken cancellationToken) => _service.GetAvailableAsync(query.PackageId,
        cancellationToken);
}
