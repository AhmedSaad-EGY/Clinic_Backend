using Clinic.Api.Contracts.Patients;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Features.Patients;
using Clinic.Domain.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
