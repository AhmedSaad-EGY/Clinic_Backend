namespace Clinic.Api.Contracts.Identity;

public sealed record CreateSecretaryRequest(
    string FullName,
    string PhoneNumber,
    string? Email,
    string UserName,
    string TemporaryPassword);
