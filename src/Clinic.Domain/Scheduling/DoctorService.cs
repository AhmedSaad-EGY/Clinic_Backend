namespace Clinic.Domain.Scheduling;

public sealed class DoctorService : Entity
{
    private DoctorService()
    {
    }

    private DoctorService(Doctor doctor, Service service)
    {
        if (doctor.DepartmentId != service.DepartmentId)
        {
            throw new DomainException("يجب أن ينتمي الطبيب والخدمة إلى نفس القسم.");
        }

        if (doctor.IsArchived || service.IsArchived || !service.IsActive)
        {
            throw new DomainException("لا يمكن إسناد خدمة غير فعالة أو طبيب مؤرشف.");
        }

        Doctor = doctor;
        DoctorId = doctor.Id;
        Service = service;
        ServiceId = service.Id;
        DepartmentId = doctor.DepartmentId;
        IsActive = true;
    }

    public long DoctorId { get; private set; }

    public Doctor Doctor { get; private set; } = null!;

    public long ServiceId { get; private set; }

    public Service Service { get; private set; } = null!;

    public long DepartmentId { get; private set; }

    public bool IsActive { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static DoctorService Create(Doctor doctor, Service service)
    {
        ArgumentNullException.ThrowIfNull(doctor);
        ArgumentNullException.ThrowIfNull(service);
        return new DoctorService(doctor, service);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
