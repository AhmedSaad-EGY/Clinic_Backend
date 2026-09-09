using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Secretaries;

public sealed record UnlockSecretaryCommand(long SecretaryUserId) : ICommand;
