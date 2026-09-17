namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/doctors")]
public sealed class AdminDoctorsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SchedulingPage<DoctorModel>>> List(
        [FromQuery] long? departmentId,
        [FromQuery] long? serviceId,
        [FromQuery] bool includeArchived,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListDoctorsQueryHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new ListDoctorsQuery(
                departmentId,
                serviceId,
                includeArchived,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize),
            token));

    [HttpGet("{doctorId:long}")]
    public async Task<ActionResult<DoctorModel>> Get(
        long doctorId,
        GetDoctorQueryHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetDoctorQuery(doctorId, true), token));

    [HttpPost]
    public async Task<ActionResult<DoctorModel>> Create(
        CreateDoctorRequest request,
        CreateDoctorCommandHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new CreateDoctorCommand(request.DepartmentId, request.Name, request.Phone), token));

    [HttpPut("{doctorId:long}")]
    public async Task<ActionResult<DoctorModel>> Update(
        long doctorId,
        UpdateDoctorRequest request,
        UpdateDoctorCommandHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new UpdateDoctorCommand(
            doctorId, request.Name, request.Phone, request.IsActive, request.RowVersion), token));

    [HttpPut("{doctorId:long}/services")]
    public async Task<ActionResult<DoctorModel>> ReplaceServices(
        long doctorId,
        ReplaceDoctorServicesRequest request,
        ReplaceDoctorServicesCommandHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ReplaceDoctorServicesCommand(
            doctorId, request.ServiceIds, request.RowVersion), token));

    [HttpPost("{doctorId:long}/archive")]
    public async Task<IActionResult> Archive(
        long doctorId,
        SchedulingRowVersionRequest request,
        ArchiveDoctorCommandHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new ArchiveDoctorCommand(doctorId, request.RowVersion), token));
}
