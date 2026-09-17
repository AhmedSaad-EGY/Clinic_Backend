namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/patients/{patientId:long}/prescriptions")]
public sealed class PatientPrescriptionsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClinicalPage<PrescriptionSummary>>> List(long patientId,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListPatientPrescriptionsQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new ListPatientPrescriptionsQuery(patientId, IncludeArchivedPatient: false,
                NormalizePage(pageNumber), NormalizeSize(pageSize)), cancellationToken));

    internal static int NormalizePage(int value) => value <= 0 ? 1 : value;
    internal static int NormalizeSize(int value) => value <= 0 ? 20 : value;
}
