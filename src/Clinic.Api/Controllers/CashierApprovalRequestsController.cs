using Clinic.Api.Contracts.Cashier;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Cashier;
using Clinic.Domain.Approvals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.SecretaryOnly)]
[Route("api/cashier/approval-requests")]
public sealed class CashierApprovalRequestsController : ControllerBase
{
    [HttpPost("appointment-cancellations")]
    public async Task<ActionResult<ApprovalRequestModel>> CreateCancellation(
        CreateCancellationApprovalRequest request,
        CreateCancellationApprovalCommandHandler handler,
        CancellationToken token)
    {
        Result<ApprovalRequestModel> result = await handler.Handle(
            new CreateCancellationApprovalCommand(request.AppointmentId,
                request.Reason, request.AppointmentRowVersion), token);
        return result.IsFailure
            ? this.ToActionResult(result)
            : CreatedAtAction(nameof(Get), new { requestId = result.Value.Id },
                result.Value);
    }

    [HttpGet("{requestId:long}")]
    public async Task<ActionResult<ApprovalRequestModel>> Get(long requestId,
        GetApprovalRequestQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new GetApprovalRequestQuery(requestId), token));

    [HttpGet]
    public async Task<ActionResult<ApprovalRequestPage>> Search(
        [FromQuery] ApprovalRequestStatus? status,
        [FromQuery] bool awaitingRefundOnly,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        SearchApprovalRequestsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new SearchApprovalRequestsQuery(
            new ApprovalRequestSearch(status, awaitingRefundOnly,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize)), token));

    [HttpPost("{requestId:long}/refunds")]
    public async Task<ActionResult<PostedRefundModel>> ExecuteRefund(long requestId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ExecuteRefundRequest request, ExecuteRefundCommandHandler handler,
        CancellationToken token)
    {
        _ = Guid.TryParse(idempotencyKey, out Guid key);
        Result<PostedRefundModel> result = await handler.Handle(
            new ExecuteRefundCommand(key, new ExecuteRefundInput(requestId,
                request.MethodAllocations.Select(item => new RefundMethodInput(
                    item.OriginalAllocationId, item.Amount,
                    item.ReferenceNumber)).ToArray(), request.Note)), token);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        if (result.Value.WasReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(result.Value);
        }

        return CreatedAtAction(nameof(CashierRefundsController.Get),
            "CashierRefunds", new { refundId = result.Value.Refund.Id },
            result.Value);
    }
}
