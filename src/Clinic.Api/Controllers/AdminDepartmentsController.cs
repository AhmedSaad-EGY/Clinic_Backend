namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/departments")]
public sealed class AdminDepartmentsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CatalogPage<DepartmentModel>>> List(
        [FromQuery] bool includeArchived,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListDepartmentsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<DepartmentModel>> result = await handler.Handle(
            new ListDepartmentsQuery(
                includeArchived,
                NormalizePageNumber(pageNumber),
                NormalizePageSize(pageSize)),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentModel>> Create(
        CreateDepartmentRequest request,
        CreateDepartmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<DepartmentModel> result = await handler.Handle(
            new CreateDepartmentCommand(request.Name, request.Description, request.RoomName),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{departmentId:long}")]
    public async Task<ActionResult<DepartmentModel>> Update(
        long departmentId,
        UpdateDepartmentRequest request,
        UpdateDepartmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<DepartmentModel> result = await handler.Handle(
            new UpdateDepartmentCommand(
                departmentId,
                request.Name,
                request.Description,
                request.RoomName,
                request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{departmentId:long}/archive")]
    public async Task<IActionResult> Archive(
        long departmentId,
        RowVersionRequest request,
        ArchiveDepartmentCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new ArchiveDepartmentCommand(departmentId, request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private static int NormalizePageNumber(int value) => value == 0 ? 1 : value;

    private static int NormalizePageSize(int value) => value == 0 ? 20 : value;
}
