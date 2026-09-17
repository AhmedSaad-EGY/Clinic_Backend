namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/packages")]
public sealed class AdminPackagesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PackagePage>> Search([FromQuery] long? departmentId,
        [FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] bool includeArchived, [FromQuery] int pageNumber,
        [FromQuery] int pageSize, SearchAdminPackagesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<PackagePage> result = await handler.Handle(new SearchAdminPackagesQuery(
            new AdminPackageFilter(departmentId, search, isActive, includeArchived,
                pageNumber == 0 ? 1 : pageNumber, pageSize == 0 ? 20 : pageSize)),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{packageId:long}")]
    public async Task<ActionResult<PackageModel>> Get(long packageId,
        GetAdminPackageQueryHandler handler, CancellationToken cancellationToken)
    {
        Result<PackageModel> result = await handler.Handle(new GetAdminPackageQuery(packageId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<PackageModel>> Create(CreatePackageRequest request,
        CreatePackageCommandHandler handler, CancellationToken cancellationToken)
    {
        Result<PackageModel> result = await handler.Handle(new CreatePackageCommand(
            request.DepartmentId, request.Name, request.BasePrice, request.ActivationGraceDays,
            request.UsageDurationDays, Map(request.Services)),
            cancellationToken);
        return result.IsFailure ? this.ToActionResult(result)
            : CreatedAtAction(nameof(Get), new { packageId = result.Value.Id }, result.Value);
    }

    [HttpPut("{packageId:long}")]
    public async Task<ActionResult<PackageModel>> Update(long packageId,
        UpdatePackageRequest request, UpdatePackageCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<PackageModel> result = await handler.Handle(new UpdatePackageCommand(packageId,
            request.Name, request.BasePrice, request.ActivationGraceDays,
            request.UsageDurationDays, Map(request.Services), request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{packageId:long}/activation")]
    public async Task<ActionResult<PackageModel>> SetActivation(long packageId,
        SetPackageActivationRequest request, SetPackageActivationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<PackageModel> result = await handler.Handle(new SetPackageActivationCommand(
            packageId, request.IsActive, request.RowVersion), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{packageId:long}/archive")]
    public async Task<IActionResult> Archive(long packageId, ArchivePackageRequest request,
        ArchivePackageCommandHandler handler, CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new ArchivePackageCommand(packageId,
            request.RowVersion), cancellationToken);
        return this.ToActionResult(result);
    }

    private static PackageServiceInput[] Map(
        IReadOnlyCollection<PackageServiceRequest>? services) => services?.Select(item =>
            new PackageServiceInput(item.ServiceId, item.SessionsIncluded,
                item.UnitPriceAtDefinition)).ToArray() ?? [];
}
