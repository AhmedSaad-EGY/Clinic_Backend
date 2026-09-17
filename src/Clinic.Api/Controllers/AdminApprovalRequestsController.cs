namespace Clinic.Api.Controllers;
[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/cashier/approval-requests")]
public sealed class AdminApprovalRequestsController : ControllerBase
{
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

    [HttpPost("{requestId:long}/approve")]
    public async Task<ActionResult<ApprovalRequestModel>> Approve(long requestId,
        ReviewApprovalRequest request, ApproveApprovalRequestCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new ApproveApprovalRequestCommand(requestId, request.Reason,
                request.RowVersion), token));

    [HttpPost("{requestId:long}/reject")]
    public async Task<ActionResult<ApprovalRequestModel>> Reject(long requestId,
        ReviewApprovalRequest request, RejectApprovalRequestCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new RejectApprovalRequestCommand(requestId, request.Reason,
                request.RowVersion), token));
}
