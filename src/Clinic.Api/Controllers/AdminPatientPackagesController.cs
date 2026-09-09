using Clinic.Api.Contracts.Packages;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Application.Features.Packages;
using Clinic.Domain.Packages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/patient-packages")]
public sealed class AdminPatientPackagesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PatientPackagePage>> Search([FromQuery] long? patientId,
        [FromQuery] long? departmentId, [FromQuery] PatientPackagePaymentStatus? paymentStatus,
        [FromQuery] PatientPackageStatus? status, [FromQuery] bool? isUsable,
        [FromQuery] string? search, [FromQuery] int pageNumber, [FromQuery] int pageSize,
        SearchAdminPatientPackagesQueryHandler handler, CancellationToken cancellationToken)
    {
        Result<PatientPackagePage> result = await handler.Handle(
            new SearchAdminPatientPackagesQuery(new AdminPatientPackageFilter(patientId,
                departmentId, paymentStatus, status, isUsable, search,
                pageNumber == 0 ? 1 : pageNumber, pageSize == 0 ? 20 : pageSize)),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{patientPackageId:long}/extend")]
    public async Task<ActionResult<PatientPackageModel>> Extend(long patientPackageId,
        ExtendPatientPackageRequest request, ExtendPatientPackageCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<PatientPackageModel> result = await handler.Handle(
            new ExtendPatientPackageCommand(patientPackageId, request.ExtensionType,
                request.NewDeadline, request.Reason, request.RowVersion), cancellationToken);
        return this.ToActionResult(result);
    }
}
