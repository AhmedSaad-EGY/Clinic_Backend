using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Packages;

public sealed record ListPatientPackagesQuery(long PatientId, int PageNumber, int PageSize)
    : IQuery<PatientPackagePage>;
public sealed record GetPatientPackageQuery(long PatientPackageId)
    : IQuery<PatientPackageModel>;
public sealed record ListPackageSessionsQuery(long PatientPackageId)
    : IQuery<IReadOnlyCollection<PackageSessionModel>>;
public sealed record GetPackageBookingOptionsQuery(long PatientPackageId,
    DateTimeOffset StartAt) : IQuery<PackageBookingOptionsModel>;
public sealed record SearchAdminPatientPackagesQuery(AdminPatientPackageFilter Filter)
    : IQuery<PatientPackagePage>;

public sealed class ListPatientPackagesQueryHandler
    : IQueryHandler<ListPatientPackagesQuery, PatientPackagePage>
{
    private readonly IPatientPackageQueryService _service;
    public ListPatientPackagesQueryHandler(IPatientPackageQueryService service) =>
        _service = service;

    public Task<Result<PatientPackagePage>> Handle(ListPatientPackagesQuery query,
        CancellationToken cancellationToken) => Validate(query.PatientId, query.PageNumber,
        query.PageSize) is { IsFailure: true } failure
        ? Task.FromResult(Result.Failure<PatientPackagePage>(failure.Error))
        : _service.ListForPatientAsync(query.PatientId, query.PageNumber, query.PageSize,
            cancellationToken);

    internal static Result Validate(long id, int pageNumber, int pageSize) =>
        id > 0 && pageNumber >= 1 && pageSize is >= 1 and <= 100
            ? Result.Success()
            : Result.Failure(PackageErrors.Validation("رقم السجل أو بيانات الصفحة غير صحيحة."));
}

public sealed class GetPatientPackageQueryHandler
    : IQueryHandler<GetPatientPackageQuery, PatientPackageModel>
{
    private readonly IPatientPackageQueryService _service;
    public GetPatientPackageQueryHandler(IPatientPackageQueryService service) => _service = service;
    public Task<Result<PatientPackageModel>> Handle(GetPatientPackageQuery query,
        CancellationToken cancellationToken) => _service.GetAsync(query.PatientPackageId,
        cancellationToken);
}

public sealed class ListPackageSessionsQueryHandler
    : IQueryHandler<ListPackageSessionsQuery, IReadOnlyCollection<PackageSessionModel>>
{
    private readonly IPatientPackageQueryService _service;
    public ListPackageSessionsQueryHandler(IPatientPackageQueryService service) =>
        _service = service;
    public Task<Result<IReadOnlyCollection<PackageSessionModel>>> Handle(
        ListPackageSessionsQuery query, CancellationToken cancellationToken) =>
        _service.ListSessionsAsync(query.PatientPackageId, cancellationToken);
}

public sealed class SearchAdminPatientPackagesQueryHandler
    : IQueryHandler<SearchAdminPatientPackagesQuery, PatientPackagePage>
{
    private readonly IPatientPackageQueryService _service;
    public SearchAdminPatientPackagesQueryHandler(IPatientPackageQueryService service) =>
        _service = service;

    public Task<Result<PatientPackagePage>> Handle(SearchAdminPatientPackagesQuery query,
        CancellationToken cancellationToken) => query.Filter.PageNumber < 1 ||
        query.Filter.PageSize is < 1 or > 100
        ? Task.FromResult(Result.Failure<PatientPackagePage>(PackageErrors.Validation(
            "بيانات الصفحة غير صحيحة.")))
        : _service.SearchAdminAsync(query.Filter, cancellationToken);
}

public sealed class GetPackageBookingOptionsQueryHandler(
    IPatientPackageQueryService service)
    : IQueryHandler<GetPackageBookingOptionsQuery, PackageBookingOptionsModel>
{
    public Task<Result<PackageBookingOptionsModel>> Handle(
        GetPackageBookingOptionsQuery query, CancellationToken cancellationToken) =>
        query.PatientPackageId <= 0
            ? Task.FromResult(Result.Failure<PackageBookingOptionsModel>(
                PackageErrors.Validation("رقم باقة المريض غير صحيح.")))
            : service.GetBookingOptionsAsync(query.PatientPackageId,
                query.StartAt.ToUniversalTime(), cancellationToken);
}
