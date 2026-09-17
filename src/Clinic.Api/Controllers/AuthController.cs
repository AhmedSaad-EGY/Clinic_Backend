namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    public ActionResult<CsrfTokenResponse> GetCsrfToken(IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(antiforgery);

        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<UserProfile>> Login(
        LoginRequest request,
        LoginCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<UserProfile> result = await handler.Handle(
            new LoginCommand(request.UserName, request.Password),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        LogoutCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(new LogoutCommand(), cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfile>> GetCurrentUser(
        GetCurrentUserQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<UserProfile> result = await handler.Handle(
            new GetCurrentUserQuery(),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserProfile>> ChangePassword(
        ChangePasswordRequest request,
        ChangePasswordCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<UserProfile> result = await handler.Handle(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword),
            cancellationToken);

        return this.ToActionResult(result);
    }
}
