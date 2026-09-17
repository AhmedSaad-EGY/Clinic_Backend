namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/prescriptions")]
public sealed class PrescriptionsController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PrescriptionModel>> Create(
        CreatePrescriptionRequest request,
        CreatePrescriptionDraftCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<PrescriptionModel> result = await handler.Handle(new CreatePrescriptionDraftCommand(
            request.AppointmentServiceId, Map(request.Content)), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { prescriptionId = result.Value.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPut("{prescriptionId:long}/draft")]
    public async Task<ActionResult<PrescriptionModel>> SaveDraft(long prescriptionId,
        SavePrescriptionDraftRequest request,
        SavePrescriptionDraftCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new SavePrescriptionDraftCommand(prescriptionId, Map(request.Content),
                request.RowVersion), cancellationToken));

    [HttpPost("{prescriptionId:long}/finalize")]
    public async Task<ActionResult<PrescriptionModel>> Finalize(long prescriptionId,
        FinalizePrescriptionRequest request,
        FinalizePrescriptionCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new FinalizePrescriptionCommand(prescriptionId,
                request.MatchesDoctorPrescription, request.RowVersion), cancellationToken));

    [HttpGet("{prescriptionId:long}")]
    public async Task<ActionResult<PrescriptionModel>> Get(long prescriptionId,
        GetPrescriptionQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(
            new GetPrescriptionQuery(prescriptionId, IncludeArchivedPatient: false),
            cancellationToken));

    internal static PrescriptionContentInput Map(PrescriptionContentRequest request) =>
        new([.. request.Items.Select(item => new PrescriptionItemInput(item.MedicineName,
            item.DoseAmount, item.DoseUnit, item.TimesPerDay, item.FrequencyText,
            item.DurationText, item.FoodTiming, item.Instructions))],
            request.ReturnDate, request.ReturnAfterDays);
}
