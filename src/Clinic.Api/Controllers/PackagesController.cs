namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/catalog/packages")]
public sealed class PackagesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PackagePage>> Search([FromQuery] long? departmentId,
        [FromQuery] string? search, [FromQuery] int pageNumber, [FromQuery] int pageSize,
        SearchAvailablePackagesQueryHandler handler, CancellationToken cancellationToken)
    {
        Result<PackagePage> result = await handler.Handle(new SearchAvailablePackagesQuery(
            new PackageCatalogFilter(departmentId, search, pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize)), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{packageId:long}")]
    public async Task<ActionResult<PackageModel>> Get(long packageId,
        GetAvailablePackageQueryHandler handler, CancellationToken cancellationToken)
    {
        Result<PackageModel> result = await handler.Handle(
            new GetAvailablePackageQuery(packageId), cancellationToken);
        return this.ToActionResult(result);
    }
}
