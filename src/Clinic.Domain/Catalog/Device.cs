namespace Clinic.Domain.Catalog;

public sealed class Device : AggregateRoot
{
    private Device()
    {
    }

    private Device(long departmentId, string name, string? identifier)
    {
        CatalogGuard.PositiveIdentifier(departmentId, "القسم");
        DepartmentId = departmentId;
        Name = CatalogGuard.RequiredText(name, "اسم الجهاز", 200);
        Identifier = CatalogGuard.OptionalText(identifier, "رقم الجهاز", 100);
        IsActive = true;
    }

    public long DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string? Identifier { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsArchived { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Device Create(long departmentId, string name, string? identifier) =>
        new(departmentId, name, identifier);

    public void Update(string name, string? identifier)
    {
        EnsureNotArchived();
        Name = CatalogGuard.RequiredText(name, "اسم الجهاز", 200);
        Identifier = CatalogGuard.OptionalText(identifier, "رقم الجهاز", 100);
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
            throw new DomainException("لا يمكن تعديل جهاز مؤرشف.");
        }
    }
}
