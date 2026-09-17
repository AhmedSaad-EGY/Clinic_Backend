namespace Clinic.Application.Features.Identity.Secretaries;

public sealed record DisableSecretaryCommand(long SecretaryUserId, string Reason) : ICommand;
