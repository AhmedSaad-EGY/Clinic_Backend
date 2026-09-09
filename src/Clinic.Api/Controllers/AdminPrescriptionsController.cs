using Clinic.Api.Contracts.ClinicalRecords;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Features.ClinicalRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/prescriptions")]
public sealed class AdminPrescriptionsController : ControllerBase
{
    [HttpPost("{prescriptionId:long}/revisions")]
    public async Task<ActionResult<PrescriptionModel>> Correct(long prescriptionId,
        CorrectPrescriptionRequest request, CorrectPrescriptionCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CorrectPrescriptionCommand(prescriptionId,
            PrescriptionsController.Map(request.Content), request.Reason,
            request.MatchesDoctorPrescription, request.RowVersion), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRevision), new
            {
                prescriptionId,
                revisionId = result.Value.CurrentRevision.Id
            }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPost("{prescriptionId:long}/void")]
    public async Task<ActionResult<PrescriptionModel>> Void(long prescriptionId,
        ClinicalReasonRequest request, VoidPrescriptionCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new VoidPrescriptionCommand(prescriptionId, request.Reason, request.RowVersion),
            cancellationToken));

    [HttpGet("{prescriptionId:long}")]
    public async Task<ActionResult<PrescriptionModel>> Get(long prescriptionId,
        GetPrescriptionQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(
            new GetPrescriptionQuery(prescriptionId, IncludeArchivedPatient: true),
            cancellationToken));

    [HttpGet("{prescriptionId:long}/revisions")]
    public async Task<ActionResult<IReadOnlyCollection<PrescriptionRevisionModel>>> Revisions(
        long prescriptionId, ListPrescriptionRevisionsQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new ListPrescriptionRevisionsQuery(prescriptionId), cancellationToken));

    [HttpGet("{prescriptionId:long}/revisions/{revisionId:long}")]
    public async Task<ActionResult<PrescriptionRevisionModel>> GetRevision(
        long prescriptionId, long revisionId,
        GetPrescriptionRevisionQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new GetPrescriptionRevisionQuery(prescriptionId, revisionId),
            cancellationToken));
}
