using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Authentication;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : ICommand<UserProfile>;
