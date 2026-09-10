using Clinic.Api.Contracts.Cashier;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Cashier;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.SecretaryOnly)]
[Route("api/cashier")]
public sealed class CashierCashWithdrawalsController : ControllerBase
{
    [HttpPost("cash-withdrawals")]
    public async Task<ActionResult<CashWithdrawalModel>> Create(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CreateCashWithdrawalRequest request,
        CreateCashWithdrawalCommandHandler handler,
        CancellationToken cancellationToken)
    {
        _ = Guid.TryParse(idempotencyKey, out Guid key);
        Result<CreatedCashWithdrawalModel> handled = await handler.Handle(
            new CreateCashWithdrawalCommand(key, request.Amount, request.Reason),
            cancellationToken);
        if (handled.IsFailure)
        {
            return this.ToActionResult(Result.Failure<CashWithdrawalModel>(
                handled.Error));
        }

        CreatedCashWithdrawalModel result = handled.Value;
        if (result.WasReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(result.Withdrawal);
        }

        return CreatedAtAction(nameof(Get),
            new { withdrawalId = result.Withdrawal.Id }, result.Withdrawal);
    }

    [HttpGet("cash-withdrawals/{withdrawalId:long}")]
    public async Task<ActionResult<CashWithdrawalModel>> Get(long withdrawalId,
        GetCashWithdrawalQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetCashWithdrawalQuery(withdrawalId,
                AdminOverride: false), cancellationToken));

    [HttpGet("shifts/{shiftId:long}/cash-withdrawals")]
    public async Task<ActionResult<CashWithdrawalPage>> ListForShift(long shiftId,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListShiftCashWithdrawalsQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new ListShiftCashWithdrawalsQuery(shiftId,
                AdminOverride: false, pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize), cancellationToken));

    [HttpPost("cash-withdrawals/{withdrawalId:long}/execute")]
    public async Task<ActionResult<ExecutedCashWithdrawalModel>> Execute(
        long withdrawalId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CashWithdrawalRowVersionRequest request,
        ExecuteCashWithdrawalCommandHandler handler,
        CancellationToken cancellationToken)
    {
        _ = Guid.TryParse(idempotencyKey, out Guid key);
        Result<ExecutedCashWithdrawalModel> result = await handler.Handle(
            new ExecuteCashWithdrawalCommand(withdrawalId, key,
                request.RowVersion), cancellationToken);
        if (result.IsSuccess && result.Value.WasReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
        }

        return this.ToActionResult(result);
    }

    [HttpPost("cash-withdrawals/{withdrawalId:long}/cancel")]
    public async Task<ActionResult<CashWithdrawalModel>> Cancel(long withdrawalId,
        CashWithdrawalRowVersionRequest request,
        CancelCashWithdrawalCommandHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new CancelCashWithdrawalCommand(withdrawalId,
                request.RowVersion), cancellationToken));
}
