namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/secretaries")]
public sealed class SecretariesController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<SecretarySummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SecretarySummary>>> List(
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListSecretariesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        int requestedPageNumber = pageNumber == 0 ? 1 : pageNumber;
        int requestedPageSize = pageSize == 0 ? 20 : pageSize;
        Result<PagedResult<SecretarySummary>> result = await handler.Handle(
            new ListSecretariesQuery(requestedPageNumber, requestedPageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<SecretarySummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SecretarySummary>> Create(
        CreateSecretaryRequest request,
        CreateSecretaryCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<SecretarySummary> result = await handler.Handle(
            new CreateSecretaryCommand(
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.UserName,
                request.TemporaryPassword),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{secretaryUserId:long}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disable(
        long secretaryUserId,
        DisableSecretaryRequest request,
        DisableSecretaryCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new DisableSecretaryCommand(secretaryUserId, request.Reason),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{secretaryUserId:long}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Enable(
        long secretaryUserId,
        EnableSecretaryCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new EnableSecretaryCommand(secretaryUserId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{secretaryUserId:long}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unlock(
        long secretaryUserId,
        UnlockSecretaryCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new UnlockSecretaryCommand(secretaryUserId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{secretaryUserId:long}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        long secretaryUserId,
        ResetSecretaryPasswordRequest request,
        ResetSecretaryPasswordCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new ResetSecretaryPasswordCommand(secretaryUserId, request.TemporaryPassword),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
