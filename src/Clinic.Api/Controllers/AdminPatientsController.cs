namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/patients")]
public sealed class AdminPatientsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PatientPage<PatientSummary>>> List(
        [FromQuery] string? search, [FromQuery] PatientGender? gender,
        [FromQuery] string? area, [FromQuery] bool includeArchived,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListPatientsQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ListPatientsQuery(search, gender, area,
            includeArchived, NormalizePageNumber(pageNumber), NormalizePageSize(pageSize)),
            cancellationToken));

    [HttpGet("{patientId:long}")]
    public async Task<ActionResult<PatientDetails>> Get(long patientId,
        GetPatientQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(
            new GetPatientQuery(patientId, IncludeArchived: true), cancellationToken));

    [HttpGet("{patientId:long}/timeline")]
    public async Task<ActionResult<PatientTimelinePage>> Timeline(long patientId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] PatientTimelineRecordType[]? recordTypes,
        [FromQuery] bool includeArchived, [FromQuery] int pageNumber,
        [FromQuery] int pageSize, GetPatientTimelineQueryHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new GetPatientTimelineQuery(patientId,
            IncludeArchivedPatient: true, IncludeAdminOnlyNotes: true,
            new PatientTimelineFilter(from, to, recordTypes ?? [], includeArchived,
                NormalizePageNumber(pageNumber), NormalizePageSize(pageSize))),
            cancellationToken));

    [HttpGet("{patientId:long}/payments/{paymentId:long}")]
    public async Task<ActionResult<PaymentModel>> Payment(long patientId, long paymentId,
        GetPatientPaymentQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new GetPatientPaymentQuery(patientId,
            paymentId, IncludeArchivedPatient: true), cancellationToken));

    [HttpGet("{patientId:long}/refunds/{refundId:long}")]
    public async Task<ActionResult<RefundModel>> Refund(long patientId, long refundId,
        GetPatientRefundQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new GetPatientRefundQuery(patientId,
            refundId, IncludeArchivedPatient: true), cancellationToken));

    [HttpGet("{patientId:long}/notes")]
    public async Task<ActionResult<PatientPage<PatientNoteModel>>> Notes(long patientId,
        [FromQuery] bool includeArchived, [FromQuery] int pageNumber,
        [FromQuery] int pageSize, ListPatientNotesQueryHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ListPatientNotesQuery(patientId,
            IncludeAdminOnly: true, includeArchived, NormalizePageNumber(pageNumber),
            NormalizePageSize(pageSize)), cancellationToken));

    [HttpGet("{patientId:long}/treatment-history")]
    public async Task<ActionResult<PatientPage<TreatmentHistoryModel>>> TreatmentHistory(
        long patientId, [FromQuery] bool includeArchived, [FromQuery] int pageNumber,
        [FromQuery] int pageSize, ListTreatmentHistoryQueryHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ListTreatmentHistoryQuery(patientId,
            includeArchived, NormalizePageNumber(pageNumber), NormalizePageSize(pageSize)),
            cancellationToken));

    [HttpPost("{patientId:long}/archive")]
    public async Task<IActionResult> ArchivePatient(long patientId,
        ArchivePatientRecordRequest request, ArchivePatientCommandHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ArchivePatientCommand(patientId,
            request.Reason, request.RowVersion), cancellationToken));

    [HttpPost("{patientId:long}/restore")]
    public async Task<ActionResult<PatientDetails>> RestorePatient(long patientId,
        ArchivePatientRecordRequest request, RestorePatientCommandHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new RestorePatientCommand(patientId,
            request.Reason, request.RowVersion), cancellationToken));

    [HttpPost("notes/{noteId:long}/archive")]
    public async Task<IActionResult> ArchiveNote(long noteId,
        ArchivePatientRecordRequest request, ArchivePatientNoteCommandHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ArchivePatientNoteCommand(noteId,
            request.Reason, request.RowVersion), cancellationToken));

    [HttpPost("treatment-history/{treatmentHistoryId:long}/archive")]
    public async Task<IActionResult> ArchiveTreatmentHistory(long treatmentHistoryId,
        ArchivePatientRecordRequest request, ArchiveTreatmentHistoryCommandHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ArchiveTreatmentHistoryCommand(
            treatmentHistoryId, request.Reason, request.RowVersion), cancellationToken));

    private static int NormalizePageNumber(int value) => value == 0 ? 1 : value;
    private static int NormalizePageSize(int value) => value == 0 ? 20 : value;
}
