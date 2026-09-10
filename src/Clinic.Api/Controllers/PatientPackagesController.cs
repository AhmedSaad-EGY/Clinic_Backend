using Clinic.Api.Contracts.Packages;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Application.Features.Packages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api")]
public sealed class PatientPackagesController : ControllerBase
{
    [HttpPost("patients/{patientId:long}/packages")]
    public async Task<ActionResult<PatientPackageModel>> Register(long patientId,
        RegisterPatientPackageRequest request,
        [FromHeader(Name = "Idempotency-Key"), BindRequired] Guid idempotencyKey,
        RegisterPatientPackageCommandHandler handler, CancellationToken cancellationToken)
    {
        Result<PatientPackageRegistrationResult> result = await handler.Handle(
            new RegisterPatientPackageCommand(patientId, request.PackageId,
                request.PackageRowVersion, idempotencyKey), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result.Failure<PatientPackageModel>(result.Error));
        }

        if (result.Value.IsReplay)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(result.Value.PatientPackage);
        }

        return CreatedAtAction(nameof(Get),
            new { patientPackageId = result.Value.PatientPackage.Id },
            result.Value.PatientPackage);
    }

    [HttpGet("patients/{patientId:long}/packages")]
    public async Task<ActionResult<PatientPackagePage>> ListForPatient(long patientId,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListPatientPackagesQueryHandler handler, CancellationToken cancellationToken)
    {
        Result<PatientPackagePage> result = await handler.Handle(new ListPatientPackagesQuery(
            patientId, pageNumber == 0 ? 1 : pageNumber, pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("patient-packages/{patientPackageId:long}")]
    public async Task<ActionResult<PatientPackageModel>> Get(long patientPackageId,
        GetPatientPackageQueryHandler handler, CancellationToken cancellationToken)
    {
        Result<PatientPackageModel> result = await handler.Handle(
            new GetPatientPackageQuery(patientPackageId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("patient-packages/{patientPackageId:long}/sessions")]
    public async Task<ActionResult<IReadOnlyCollection<PackageSessionModel>>> ListSessions(
        long patientPackageId, ListPackageSessionsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyCollection<PackageSessionModel>> result = await handler.Handle(
            new ListPackageSessionsQuery(patientPackageId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("patient-packages/{patientPackageId:long}/booking-options")]
    public async Task<ActionResult<PackageBookingOptionsModel>> BookingOptions(
        long patientPackageId, [FromQuery] DateTimeOffset startAt,
        GetPackageBookingOptionsQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new GetPackageBookingOptionsQuery(
            patientPackageId, startAt), cancellationToken));
}
