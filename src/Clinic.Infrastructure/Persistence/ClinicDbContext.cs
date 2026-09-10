using Clinic.Application.Abstractions.Persistence;
using Clinic.Domain.Appointments;
using Clinic.Domain.Approvals;
using Clinic.Domain.Auditing;
using Clinic.Domain.Cashier;
using Clinic.Domain.Catalog;
using Clinic.Domain.ClinicalRecords;
using Clinic.Domain.Discounts;
using Clinic.Domain.Patients;
using Clinic.Domain.Packages;
using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Persistence;

public sealed class ClinicDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, long>, IUnitOfWork
{
    public ClinicDbContext(DbContextOptions<ClinicDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Specialization> Specializations => Set<Specialization>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<ServicePriceHistory> ServicePriceHistory => Set<ServicePriceHistory>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<ServiceDevice> ServiceDevices => Set<ServiceDevice>();

    public DbSet<Package> Packages => Set<Package>();

    public DbSet<PackageService> PackageServices => Set<PackageService>();

    public DbSet<PatientPackage> PatientPackages => Set<PatientPackage>();

    public DbSet<PatientPackageService> PatientPackageServices => Set<PatientPackageService>();

    public DbSet<PackageSession> PackageSessions => Set<PackageSession>();

    public DbSet<PackageSessionBooking> PackageSessionBookings => Set<PackageSessionBooking>();

    public DbSet<Discount> Discounts => Set<Discount>();

    public DbSet<DiscountDepartment> DiscountDepartments => Set<DiscountDepartment>();

    public DbSet<DiscountService> DiscountServices => Set<DiscountService>();

    public DbSet<DiscountPackage> DiscountPackages => Set<DiscountPackage>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<DoctorService> DoctorServices => Set<DoctorService>();

    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();

    public DbSet<DoctorScheduleOverride> DoctorExceptions => Set<DoctorScheduleOverride>();

    public DbSet<DepartmentClosure> DepartmentClosures => Set<DepartmentClosure>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<PatientNote> PatientNotes => Set<PatientNote>();

    public DbSet<TreatmentHistory> TreatmentHistory => Set<TreatmentHistory>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();

    public DbSet<AppointmentDevice> AppointmentDevices => Set<AppointmentDevice>();

    public DbSet<CashDrawer> CashDrawers => Set<CashDrawer>();

    public DbSet<ShiftPolicy> ShiftPolicies => Set<ShiftPolicy>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentMethodAllocation> PaymentMethodAllocations =>
        Set<PaymentMethodAllocation>();

    public DbSet<AppointmentPaymentAllocation> AppointmentPaymentAllocations =>
        Set<AppointmentPaymentAllocation>();

    public DbSet<PackagePaymentAllocation> PackagePaymentAllocations =>
        Set<PackagePaymentAllocation>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<RefundMethodAllocation> RefundMethodAllocations =>
        Set<RefundMethodAllocation>();

    public DbSet<RefundAppointmentAllocation> RefundAppointmentAllocations =>
        Set<RefundAppointmentAllocation>();

    public DbSet<Prescription> Prescriptions => Set<Prescription>();

    public DbSet<PrescriptionRevision> PrescriptionRevisions =>
        Set<PrescriptionRevision>();

    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    public DbSet<FollowUp> FollowUps => Set<FollowUp>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.HasSequence<long>("PatientFileNumberSequence", "patients")
            .StartsAt(1)
            .IncrementsBy(1);

        builder.HasSequence<long>("PaymentTransactionNumberSequence", "cashier")
            .StartsAt(1)
            .IncrementsBy(1);

        builder.HasSequence<long>("RefundTransactionNumberSequence", "cashier")
            .StartsAt(1)
            .IncrementsBy(1);

        builder.Entity<IdentityUserClaim<long>>().ToTable("UserClaims", "identity");
        builder.Entity<IdentityUserLogin<long>>().ToTable("UserLogins", "identity");
        builder.Entity<IdentityUserRole<long>>().ToTable("UserRoles", "identity");
        builder.Entity<IdentityUserToken<long>>().ToTable("UserTokens", "identity");
        builder.Entity<IdentityRoleClaim<long>>().ToTable("RoleClaims", "identity");
        builder.ApplyConfigurationsFromAssembly(typeof(ClinicDbContext).Assembly);
    }
}
