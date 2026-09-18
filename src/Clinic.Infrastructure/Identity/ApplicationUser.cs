namespace Clinic.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<long>
{
    public string FullName { get; set; } = string.Empty;

    public string? NormalizedPhoneNumber { get; set; }

    public bool MustChangePassword { get; set; }

    public bool IsDisabled { get; set; }

    public bool IsArchived { get; set; }

    public long? CreatedByAdminUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
