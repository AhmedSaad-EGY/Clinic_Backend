using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Features.Cashier;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/cashier/payment-methods")]
public sealed class PaymentMethodsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PaymentMethodModel>>> List(
        ListPaymentMethodsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ListPaymentMethodsQuery(), token));
}
