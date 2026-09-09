using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Features.Cashier;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/cashier")]
public sealed class AdminRefundsController : ControllerBase
{
    [HttpGet("refunds/{refundId:long}")]
    public async Task<ActionResult<RefundModel>> Get(long refundId,
        GetRefundQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetRefundQuery(refundId,
            AdminOverride: true), token));

    [HttpGet("shifts/{shiftId:long}/refunds")]
    public async Task<ActionResult<RefundPage>> ListForShift(long shiftId,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListShiftRefundsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ListShiftRefundsQuery(shiftId,
            AdminOverride: true, pageNumber == 0 ? 1 : pageNumber,
            pageSize == 0 ? 20 : pageSize), token));
}
