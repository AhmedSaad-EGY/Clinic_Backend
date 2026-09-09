namespace Clinic.Api.Contracts.Identity;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
