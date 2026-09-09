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
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/cashier")]
public sealed class AdminPaymentsController : ControllerBase
{
    [HttpPost("payments")]
    public async Task<ActionResult<PaymentModel>> Post(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        AdminPostPaymentRequest request, PostPaymentCommandHandler handler,
        CancellationToken token)
    {
        _ = Guid.TryParse(idempotencyKey, out Guid key);
        Result<PostedPaymentModel> handled = await handler.Handle(new PostPaymentCommand(
            key, Map(request), AdminOverride: true), token);
        if (handled.IsFailure)
        {
            return this.ToActionResult(Result.Failure<PaymentModel>(handled.Error));
        }

        PostedPaymentModel result = handled.Value;
        if (result.WasReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(result.Payment);
        }

        return CreatedAtAction(nameof(Get), new { paymentId = result.Payment.Id },
            result.Payment);
    }

    [HttpGet("payments/{paymentId:long}")]
    public async Task<ActionResult<PaymentModel>> Get(long paymentId,
        GetPaymentQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetPaymentQuery(paymentId,
            AdminOverride: true), token));

    [HttpGet("shifts/{shiftId:long}/payments")]
    public async Task<ActionResult<PaymentPage>> ListForShift(long shiftId,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListShiftPaymentsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ListShiftPaymentsQuery(shiftId,
            AdminOverride: true, pageNumber == 0 ? 1 : pageNumber,
            pageSize == 0 ? 20 : pageSize), token));

    [HttpGet("shifts/{shiftId:long}/collection-summary")]
    public async Task<ActionResult<ShiftCollectionSummaryModel>> Summary(long shiftId,
        GetShiftCollectionSummaryQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetShiftCollectionSummaryQuery(
            shiftId, AdminOverride: true), token));

    private static PostPaymentInput Map(AdminPostPaymentRequest request) => new(
        request.ShiftId, request.AppointmentIds,
        request.MethodAllocations.Select(item => new PaymentMethodInput(
            item.PaymentMethodId, item.Amount, item.ReferenceNumber)).ToArray(),
        request.Note, request.Reason);
}
