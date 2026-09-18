namespace Clinic.Domain.Catalog;

public sealed class Department : AggregateRoot
{
    private Department()
    {
    }

    private Department(
        string name,
        string? description,
        string roomName,
        DateTimeOffset createdAt)
    {
        Name = CatalogGuard.RequiredText(name, "اسم القسم", 150);
        Description = CatalogGuard.OptionalText(description, "وصف القسم", 1000);
        CreatedAt = createdAt;
        Room = new Room(this, roomName);
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public Room Room { get; private set; } = null!;

    public static Department Create(
        string name,
        string? description,
        string roomName,
        DateTimeOffset createdAt) =>
        new(name, description, roomName, createdAt);

    public void Update(
        string name,
        string? description,
        string roomName,
        DateTimeOffset updatedAt)
    {
        EnsureNotArchived();
        Name = CatalogGuard.RequiredText(name, "اسم القسم", 150);
        Description = CatalogGuard.OptionalText(description, "وصف القسم", 1000);
        Room.Rename(roomName);
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset updatedAt)
    {
        EnsureNotArchived();
        IsArchived = true;
        Room.Archive();
        UpdatedAt = updatedAt;
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new DomainException("لا يمكن تعديل قسم مؤرشف.");
        }
    }
}
