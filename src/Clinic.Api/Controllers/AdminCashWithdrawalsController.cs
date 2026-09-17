namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/cashier/cash-withdrawals")]
public sealed class AdminCashWithdrawalsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CashWithdrawalPage>> Search(
        [FromQuery] CashWithdrawalSearchRequest request,
        SearchCashWithdrawalsQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new SearchCashWithdrawalsQuery(
                new CashWithdrawalSearch(request.ShiftId,
                    request.SecretaryUserId, request.Status, request.From,
                    request.To, request.PageNumber, request.PageSize)),
                cancellationToken));

    [HttpGet("{withdrawalId:long}")]
    public async Task<ActionResult<CashWithdrawalModel>> Get(long withdrawalId,
        GetCashWithdrawalQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetCashWithdrawalQuery(withdrawalId,
                AdminOverride: true), cancellationToken));

    [HttpPost("{withdrawalId:long}/approve")]
    public async Task<ActionResult<CashWithdrawalModel>> Approve(long withdrawalId,
        ReviewCashWithdrawalRequest request,
        ApproveCashWithdrawalCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new ApproveCashWithdrawalCommand(withdrawalId,
                request.Reason, request.RowVersion), cancellationToken));

    [HttpPost("{withdrawalId:long}/reject")]
    public async Task<ActionResult<CashWithdrawalModel>> Reject(long withdrawalId,
        ReviewCashWithdrawalRequest request,
        RejectCashWithdrawalCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new RejectCashWithdrawalCommand(withdrawalId,
                request.Reason, request.RowVersion), cancellationToken));
}
