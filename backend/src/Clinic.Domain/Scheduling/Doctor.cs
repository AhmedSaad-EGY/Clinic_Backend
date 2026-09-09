using Clinic.Domain.Catalog;
using Clinic.Domain.Common;

namespace Clinic.Domain.Scheduling;

public sealed class Doctor : AggregateRoot
{
    private Doctor()
    {
    }

    private Doctor(long departmentId, string name, string? phone, DateTimeOffset createdAt)
    {
        SchedulingGuard.PositiveId(departmentId, "القسم");
        DepartmentId = departmentId;
        Name = SchedulingGuard.RequiredText(name, "اسم الطبيب", 150);
        Phone = SchedulingGuard.OptionalText(phone, "رقم الهاتف", 30);
        IsActive = true;
        CreatedAt = createdAt;
    }

    public long DepartmentId { get; private set; }

    public Department Department { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Doctor Create(
        long departmentId,
        string name,
        string? phone,
        DateTimeOffset createdAt) => new(departmentId, name, phone, createdAt);

    public void Update(string name, string? phone, bool isActive, DateTimeOffset updatedAt)
    {
        EnsureNotArchived();
        Name = SchedulingGuard.RequiredText(name, "اسم الطبيب", 150);
        Phone = SchedulingGuard.OptionalText(phone, "رقم الهاتف", 30);
        IsActive = isActive;
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        EnsureNotArchived();
        IsActive = false;
        IsArchived = true;
        UpdatedAt = archivedAt;
    }

    public void Touch(DateTimeOffset updatedAt)
    {
        EnsureNotArchived();
        UpdatedAt = updatedAt;
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new DomainException("لا يمكن تعديل طبيب مؤرشف.");
        }
    }
}
