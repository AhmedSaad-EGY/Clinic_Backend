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
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PatientPage<PatientSummary>>> List(
        [FromQuery] string? search, [FromQuery] PatientGender? gender,
        [FromQuery] string? area, [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListPatientsQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ListPatientsQuery(search, gender, area,
            IncludeArchived: false, NormalizePageNumber(pageNumber), NormalizePageSize(pageSize)),
            cancellationToken));

    [HttpGet("{patientId:long}")]
    public async Task<ActionResult<PatientDetails>> Get(long patientId,
        GetPatientQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(
            new GetPatientQuery(patientId, IncludeArchived: false), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PatientDetails>> Create(CreatePatientRequest request,
        CreatePatientCommandHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(
            new CreatePatientCommand(ToInput(request)), cancellationToken));

    [HttpPut("{patientId:long}")]
    public async Task<ActionResult<PatientDetails>> Update(long patientId,
        UpdatePatientRequest request, UpdatePatientCommandHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new UpdatePatientCommand(patientId,
            ToInput(request), request.RowVersion), cancellationToken));

    [HttpGet("{patientId:long}/notes")]
    public async Task<ActionResult<PatientPage<PatientNoteModel>>> Notes(long patientId,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListPatientNotesQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ListPatientNotesQuery(patientId,
            IncludeAdminOnly: false, IncludeArchived: false, NormalizePageNumber(pageNumber),
            NormalizePageSize(pageSize)), cancellationToken));

    [HttpPost("{patientId:long}/notes")]
    public async Task<ActionResult<SensitiveNoteReceipt>> CreateNote(long patientId,
        CreatePatientNoteRequest request, CreatePatientNoteCommandHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new CreatePatientNoteCommand(patientId,
            request.NoteText, request.Visibility), cancellationToken));

    [HttpGet("{patientId:long}/treatment-history")]
    public async Task<ActionResult<PatientPage<TreatmentHistoryModel>>> TreatmentHistory(
        long patientId, [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListTreatmentHistoryQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ListTreatmentHistoryQuery(patientId,
            IncludeArchived: false, NormalizePageNumber(pageNumber), NormalizePageSize(pageSize)),
            cancellationToken));

    [HttpPost("{patientId:long}/treatment-history")]
    public async Task<ActionResult<TreatmentHistoryModel>> CreateTreatmentHistory(long patientId,
        CreateTreatmentHistoryRequest request, CreateTreatmentHistoryCommandHandler handler,
        CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new CreateTreatmentHistoryCommand(patientId,
            request.EventDate, request.Description), cancellationToken));

    private static PatientInput ToInput(CreatePatientRequest request) => new(request.FullName,
        request.PrimaryPhoneNumber, request.SecondaryPhoneNumber, request.BirthDate, request.Age,
        request.Gender, request.Area, request.Address, request.Email, request.GuardianName,
        request.GuardianPhoneNumber);

    private static PatientInput ToInput(UpdatePatientRequest request) => new(request.FullName,
        request.PrimaryPhoneNumber, request.SecondaryPhoneNumber, request.BirthDate, request.Age,
        request.Gender, request.Area, request.Address, request.Email, request.GuardianName,
        request.GuardianPhoneNumber);

    private static int NormalizePageNumber(int value) => value == 0 ? 1 : value;
    private static int NormalizePageSize(int value) => value == 0 ? 20 : value;
}
