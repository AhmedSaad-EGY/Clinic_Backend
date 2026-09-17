namespace Clinic.Application.Features.Identity.Authentication;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : ICommand<UserProfile>;
