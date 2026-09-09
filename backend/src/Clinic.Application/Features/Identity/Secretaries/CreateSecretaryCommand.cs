using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Identity.Secretaries;

public sealed record CreateSecretaryCommand(
    string FullName,
    string PhoneNumber,
    string? Email,
    string UserName,
    string TemporaryPassword) : ICommand<SecretarySummary>;
