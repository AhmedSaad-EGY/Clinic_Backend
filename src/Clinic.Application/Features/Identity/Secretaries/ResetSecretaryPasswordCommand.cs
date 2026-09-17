namespace Clinic.Application.Features.Identity.Secretaries;

public sealed record ResetSecretaryPasswordCommand(
    long SecretaryUserId,
    string TemporaryPassword) : ICommand;
