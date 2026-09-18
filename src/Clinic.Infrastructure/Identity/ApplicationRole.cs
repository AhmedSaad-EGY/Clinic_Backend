namespace Clinic.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<long>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }
}
