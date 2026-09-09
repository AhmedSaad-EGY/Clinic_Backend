namespace Clinic.Infrastructure.Identity;

internal static class IdentityAuditActions
{
    public const string LoginSucceeded = "identity.login_succeeded";
    public const string LoginFailed = "identity.login_failed";
    public const string LoginRejectedDisabled = "identity.login_rejected_disabled";
    public const string LoginRejectedLocked = "identity.login_rejected_locked";
    public const string AccountLocked = "identity.account_locked";
    public const string PasswordChanged = "identity.password_changed";
    public const string SecretaryCreated = "identity.secretary_created";
    public const string SecretaryDisabled = "identity.secretary_disabled";
    public const string SecretaryEnabled = "identity.secretary_enabled";
    public const string SecretaryUnlocked = "identity.secretary_unlocked";
    public const string SecretaryPasswordReset = "identity.secretary_password_reset";
}
