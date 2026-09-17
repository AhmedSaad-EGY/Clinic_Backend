namespace Clinic.Application.Features.Identity.Authentication;

public sealed record LoginCommand(string UserName, string Password) : ICommand<UserProfile>;
