namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/follow-ups")]
public sealed class AdminFollowUpsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClinicalPage<FollowUpModel>>> Search(
        [FromQuery] long? patientId, [FromQuery] long? departmentId,
        [FromQuery] long? doctorId, [FromQuery] FollowUpStatus? status,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] bool overdueOnly, [FromQuery] int pageNumber,
        [FromQuery] int pageSize, SearchFollowUpsQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new SearchFollowUpsQuery(new FollowUpSearch(patientId, departmentId, doctorId,
                status, fromDate, toDate, overdueOnly, IncludeArchivedPatient: true,
                PatientPrescriptionsController.NormalizePage(pageNumber),
                PatientPrescriptionsController.NormalizeSize(pageSize))), cancellationToken));

    [HttpGet("{followUpId:long}")]
    public async Task<ActionResult<FollowUpModel>> Get(long followUpId,
        GetFollowUpQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(
            new GetFollowUpQuery(followUpId, IncludeArchivedPatient: true),
            cancellationToken));
}
