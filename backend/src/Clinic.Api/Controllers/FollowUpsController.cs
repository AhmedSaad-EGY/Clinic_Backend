using Clinic.Api.Contracts.ClinicalRecords;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Features.ClinicalRecords;
using Clinic.Domain.ClinicalRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/follow-ups")]
public sealed class FollowUpsController : ControllerBase
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
                status, fromDate, toDate, overdueOnly, IncludeArchivedPatient: false,
                PatientPrescriptionsController.NormalizePage(pageNumber),
                PatientPrescriptionsController.NormalizeSize(pageSize))), cancellationToken));

    [HttpGet("{followUpId:long}")]
    public async Task<ActionResult<FollowUpModel>> Get(long followUpId,
        GetFollowUpQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(
            new GetFollowUpQuery(followUpId, IncludeArchivedPatient: false),
            cancellationToken));

    [HttpPost("{followUpId:long}/cancel")]
    public async Task<ActionResult<FollowUpModel>> Cancel(long followUpId,
        ClinicalReasonRequest request, CancelFollowUpCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new CancelFollowUpCommand(followUpId, request.Reason, request.RowVersion),
            cancellationToken));
}
