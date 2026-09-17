namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/cashier")]
public sealed class AdminCashierController : ControllerBase
{
    [HttpGet("shift-policy")]
    public async Task<ActionResult<ShiftPolicyModel>> Policy(
        GetShiftPolicyQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetShiftPolicyQuery(), token));

    [HttpPut("shift-policy")]
    public async Task<ActionResult<ShiftPolicyModel>> UpdatePolicy(
        UpdateShiftPolicyRequest request, UpdateShiftPolicyCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new UpdateShiftPolicyCommand(request.ClosingGraceMinutes,
                request.RowVersion), token));

    [HttpGet("drawers")]
    public async Task<ActionResult<CashDrawerPage>> Drawers(
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListCashDrawersQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ListCashDrawersQuery(
            pageNumber == 0 ? 1 : pageNumber,
            pageSize == 0 ? 20 : pageSize), token));

    [HttpGet("shifts")]
    public async Task<ActionResult<ShiftPage>> Shifts(
        [FromQuery] ShiftSearchRequest request,
        SearchShiftsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new SearchShiftsQuery(
            new ShiftSearch(request.SecretaryUserId, request.FromDate,
                request.ToDate, request.Status, request.PageNumber,
                request.PageSize)), token));

    [HttpGet("shifts/{shiftId:long}")]
    public async Task<ActionResult<ShiftModel>> Shift(long shiftId,
        GetShiftQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetShiftQuery(shiftId), token));

    [HttpPost("shifts/generate")]
    public async Task<ActionResult<GeneratedShifts>> Generate(
        GenerateShiftsRequest request, GenerateShiftsCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new GenerateShiftsCommand(new GenerateShiftsInput(
                request.SecretaryUserId,
                request.FromDate,
                request.ToDate,
                request.DaysOfWeek ?? [],
                request.StartTime,
                request.EndTime)), token));

    [HttpPut("shifts/{shiftId:long}")]
    public async Task<ActionResult<ShiftModel>> Update(long shiftId,
        UpdateShiftRequest request, UpdateShiftCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new UpdateShiftCommand(shiftId, new UpdateShiftInput(
                request.Date, request.StartTime, request.EndTime),
                request.RowVersion), token));

    [HttpPost("shifts/{shiftId:long}/extend")]
    public async Task<ActionResult<ShiftModel>> Extend(long shiftId,
        ExtendShiftRequest request, ExtendShiftCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new ExtendShiftCommand(shiftId, request.NewScheduledEnd,
                request.Reason, request.RowVersion), token));

    [HttpPost("shifts/{shiftId:long}/cancel")]
    public async Task<IActionResult> Cancel(long shiftId,
        CancelShiftRequest request, CancelShiftCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new CancelShiftCommand(shiftId, request.Reason,
                request.RowVersion), token));

    [HttpPost("shifts/{shiftId:long}/opening-balance")]
    public async Task<ActionResult<ShiftModel>> OpeningBalance(long shiftId,
        OpeningBalanceRequest request, RecordOpeningBalanceCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new RecordOpeningBalanceCommand(shiftId, request.Amount,
                AdminOverride: true, request.Reason, request.RowVersion), token));

    [HttpPost("shifts/{shiftId:long}/reconcile")]
    public async Task<ActionResult<ShiftModel>> Reconcile(long shiftId,
        ReconcileShiftRequest request, ReconcileShiftCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new ReconcileShiftCommand(shiftId, request.DeclaredCash,
                AdminOverride: true, request.Reason, request.RowVersion), token));

    [HttpPost("shifts/{shiftId:long}/close")]
    public async Task<ActionResult<ShiftModel>> Close(long shiftId,
        CloseShiftRequest request, CloseShiftCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new CloseShiftCommand(shiftId, AdminOverride: true,
                request.Reason, request.RowVersion), token));
}
