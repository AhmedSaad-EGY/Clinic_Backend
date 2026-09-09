namespace Clinic.Application.Abstractions.Identity;

public sealed record UserProfile(
    long Id,
    string FullName,
    string UserName,
    string Role,
    bool MustChangePassword);
