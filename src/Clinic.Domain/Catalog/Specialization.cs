namespace Clinic.Domain.Catalog;

public sealed class Specialization : AggregateRoot
{
    private Specialization()
    {
    }

    private Specialization(long departmentId, string name)
    {
        CatalogGuard.PositiveIdentifier(departmentId, "القسم");
        DepartmentId = departmentId;
        Name = CatalogGuard.RequiredText(name, "اسم التخصص", 150);
        IsActive = true;
    }

    public long DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool IsArchived { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Specialization Create(long departmentId, string name) =>
        new(departmentId, name);

    public void Rename(string name)
    {
        EnsureNotArchived();
        Name = CatalogGuard.RequiredText(name, "اسم التخصص", 150);
    }

    public void Archive()
    {
        EnsureNotArchived();
        IsActive = false;
        IsArchived = true;
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new DomainException("لا يمكن تعديل تخصص مؤرشف.");
        }
    }
}
