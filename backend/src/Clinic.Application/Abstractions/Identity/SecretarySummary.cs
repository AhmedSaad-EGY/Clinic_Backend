namespace Clinic.Application.Abstractions.Identity;

public sealed record SecretarySummary(
    long Id,
    string FullName,
    string UserName,
    string PhoneNumber,
    string? Email,
    bool IsDisabled,
    bool IsLocked,
    DateTimeOffset CreatedAt);
