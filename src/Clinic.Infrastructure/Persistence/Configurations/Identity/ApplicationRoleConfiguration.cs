namespace Clinic.Infrastructure.Persistence.Configurations.Identity;

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles", "identity");

        builder.HasData(
            new ApplicationRole
            {
                Id = 1,
                Name = RoleNames.Admin,
                NormalizedName = RoleNames.Admin.ToUpperInvariant(),
                ConcurrencyStamp = "d6cd36ef-fd9c-4904-a681-e11d39483fbd"
            },
            new ApplicationRole
            {
                Id = 2,
                Name = RoleNames.Secretary,
                NormalizedName = RoleNames.Secretary.ToUpperInvariant(),
                ConcurrencyStamp = "a90d9701-1d48-4d37-81b9-7d5051f4bc91"
            });
    }
}
