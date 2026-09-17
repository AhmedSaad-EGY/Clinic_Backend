namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/cashier/shifts")]
public sealed class CashierShiftsController : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<ShiftModel>> Current(
        GetCurrentShiftQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetCurrentShiftQuery(), token));

    [HttpGet("history")]
    public async Task<ActionResult<ShiftPage>> History(
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        GetShiftHistoryQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetShiftHistoryQuery(
            pageNumber == 0 ? 1 : pageNumber,
            pageSize == 0 ? 20 : pageSize), token));

    [HttpPost("{shiftId:long}/opening-balance")]
    public async Task<ActionResult<ShiftModel>> OpeningBalance(long shiftId,
        OpeningBalanceRequest request, RecordOpeningBalanceCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new RecordOpeningBalanceCommand(shiftId, request.Amount,
                AdminOverride: false, Reason: null, request.RowVersion), token));

    [HttpPost("{shiftId:long}/reconcile")]
    public async Task<ActionResult<ShiftModel>> Reconcile(long shiftId,
        ReconcileShiftRequest request, ReconcileShiftCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new ReconcileShiftCommand(shiftId, request.DeclaredCash,
                AdminOverride: false, Reason: null, request.RowVersion), token));

    [HttpPost("{shiftId:long}/close")]
    public async Task<ActionResult<ShiftModel>> Close(long shiftId,
        CloseShiftRequest request, CloseShiftCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new CloseShiftCommand(shiftId, AdminOverride: false,
                request.Reason, request.RowVersion), token));
}
