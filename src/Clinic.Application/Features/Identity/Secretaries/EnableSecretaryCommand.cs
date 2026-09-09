using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Secretaries;

public sealed record EnableSecretaryCommand(long SecretaryUserId) : ICommand;
