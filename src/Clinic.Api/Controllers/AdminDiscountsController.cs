using Clinic.Api.Contracts.Discounts;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Discounts;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Discounts;
using Clinic.Domain.Discounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/discounts")]
public sealed class AdminDiscountsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DiscountPage>> Search([FromQuery] string? search,
        [FromQuery] DiscountType? type, [FromQuery] DiscountAppliesTo? appliesTo,
        [FromQuery] long? departmentId, [FromQuery] bool? isActive,
        [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] bool includeArchived,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        SearchDiscountsQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new SearchDiscountsQuery(new DiscountFilter(
            search, type, appliesTo, departmentId, isActive, effectiveAt, includeArchived,
            pageNumber == 0 ? 1 : pageNumber, pageSize == 0 ? 20 : pageSize)),
            cancellationToken));

    [HttpGet("{discountId:long}")]
    public async Task<ActionResult<DiscountModel>> Get(long discountId,
        GetDiscountQueryHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new GetDiscountQuery(discountId),
            cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DiscountModel>> Create(CreateDiscountRequest request,
        CreateDiscountCommandHandler handler, CancellationToken cancellationToken)
    {
        Result<DiscountModel> result = await handler.Handle(new CreateDiscountCommand(
            Map(request.Name, request.Type, request.Value, request.AppliesTo,
                request.ScopeMode, request.StartAt, request.EndAt, request.Targets)),
            cancellationToken);
        return result.IsFailure ? this.ToActionResult(result)
            : CreatedAtAction(nameof(Get), new { discountId = result.Value.Id }, result.Value);
    }

    [HttpPut("{discountId:long}")]
    public async Task<ActionResult<DiscountModel>> Update(long discountId,
        UpdateDiscountRequest request, UpdateDiscountCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new UpdateDiscountCommand(discountId, Map(request.Name, request.Type, request.Value,
                request.AppliesTo, request.ScopeMode, request.StartAt, request.EndAt,
                request.Targets), request.RowVersion), cancellationToken));

    [HttpPut("{discountId:long}/activation")]
    public async Task<ActionResult<DiscountModel>> SetActivation(long discountId,
        SetDiscountActivationRequest request, SetDiscountActivationCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(await handler.Handle(
            new SetDiscountActivationCommand(discountId, request.IsActive, request.RowVersion),
            cancellationToken));

    [HttpPost("{discountId:long}/archive")]
    public async Task<IActionResult> Archive(long discountId, ArchiveDiscountRequest request,
        ArchiveDiscountCommandHandler handler, CancellationToken cancellationToken) =>
        this.ToActionResult(await handler.Handle(new ArchiveDiscountCommand(discountId,
            request.RowVersion), cancellationToken));

    private static DiscountDefinition Map(string name, DiscountType type, decimal value,
        DiscountAppliesTo appliesTo, DiscountScopeMode scopeMode,
        DateTimeOffset startAt, DateTimeOffset endAt, DiscountTargetsRequest? targets) =>
        new(name, type, value, appliesTo, scopeMode, startAt, endAt,
            new DiscountTargetInput(targets?.DepartmentIds ?? [], targets?.ServiceIds ?? [],
                targets?.PackageIds ?? []));
}
