namespace Clinic.Application.Abstractions.Identity;

public static class AuthorizationPolicyNames
{
    public const string AdminOnly = "AdminOnly";
    public const string SecretaryOnly = "SecretaryOnly";
    public const string PasswordChanged = "PasswordChanged";
}
