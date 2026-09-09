using Clinic.Api.Contracts.Catalog;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Catalog.Queries;
using Clinic.Application.Features.Catalog.Specializations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin")]
public sealed class AdminSpecializationsController : ControllerBase
{
    [HttpGet("departments/{departmentId:long}/specializations")]
    public async Task<ActionResult<CatalogPage<SpecializationModel>>> List(
        long departmentId,
        [FromQuery] bool includeArchived,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListSpecializationsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<SpecializationModel>> result = await handler.Handle(
            new ListSpecializationsQuery(
                departmentId,
                includeArchived,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("departments/{departmentId:long}/specializations")]
    public async Task<ActionResult<SpecializationModel>> Create(
        long departmentId,
        CreateSpecializationRequest request,
        CreateSpecializationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<SpecializationModel> result = await handler.Handle(
            new CreateSpecializationCommand(departmentId, request.Name),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("specializations/{specializationId:long}")]
    public async Task<ActionResult<SpecializationModel>> Update(
        long specializationId,
        UpdateSpecializationRequest request,
        UpdateSpecializationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<SpecializationModel> result = await handler.Handle(
            new UpdateSpecializationCommand(
                specializationId,
                request.Name,
                request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("specializations/{specializationId:long}/archive")]
    public async Task<IActionResult> Archive(
        long specializationId,
        RowVersionRequest request,
        ArchiveSpecializationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new ArchiveSpecializationCommand(specializationId, request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }
}
