using Clinic.Domain.Common;

namespace Clinic.Domain.Catalog;

public sealed class Room : Entity
{
    private Room()
    {
    }

    internal Room(Department department, string name)
    {
        ArgumentNullException.ThrowIfNull(department);
        Department = department;
        Name = CatalogGuard.RequiredText(name, "اسم الغرفة", 150);
        IsActive = true;
    }

    public long DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool IsArchived { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    internal void Rename(string name) =>
        Name = CatalogGuard.RequiredText(name, "اسم الغرفة", 150);

    internal void Archive()
    {
        IsActive = false;
        IsArchived = true;
    }
}
