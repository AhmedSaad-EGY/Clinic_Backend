using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Abstractions.Discounts;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Abstractions.Persistence;
using Clinic.Application.Abstractions.Reporting;
using Clinic.Application.Abstractions.Scheduling;
using Clinic.Infrastructure.Appointments;
using Clinic.Infrastructure.Cashier;
using Clinic.Infrastructure.Catalog;
using Clinic.Infrastructure.ClinicalRecords;
using Clinic.Infrastructure.Discounts;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Patients;
using Clinic.Infrastructure.Packages;
using Clinic.Infrastructure.Persistence;
using Clinic.Infrastructure.Reporting;
using Clinic.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string connectionString = configuration.GetConnectionString("ClinicDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'ClinicDatabase' is not configured.");

        services.AddDbContext<ClinicDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<ClinicDbContext>());

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(ConfigureIdentity)
            .AddEntityFrameworkStores<ClinicDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.Zero);

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "clinic.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ISecretaryAccountService, SecretaryAccountService>();
        services.AddScoped<IDepartmentCatalogService, DepartmentCatalogService>();
        services.AddScoped<ISpecializationCatalogService, SpecializationCatalogService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IDeviceCatalogService, DeviceCatalogService>();
        services.AddScoped<ICatalogQueryService, CatalogQueryService>();
        services.AddScoped<IDoctorAdministrationService, DoctorAdministrationService>();
        services.AddScoped<IScheduleAdministrationService, ScheduleAdministrationService>();
        services.AddScoped<ISchedulingQueryService, SchedulingQueryService>();
        services.AddScoped<IPatientAdministrationService, PatientAdministrationService>();
        services.AddScoped<IPatientQueryService, PatientQueryService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<AppointmentImpactService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IApprovalRequestService, ApprovalRequestService>();
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<ICashWithdrawalService, CashWithdrawalService>();
        services.AddScoped<IPrescriptionCommandService, PrescriptionCommandService>();
        services.AddScoped<IFollowUpCommandService, FollowUpCommandService>();
        services.AddScoped<IClinicalRecordQueryService, ClinicalRecordQueryService>();
        services.AddScoped<IPackageCommandService, PackageCommandService>();
        services.AddScoped<IPackageQueryService, PackageQueryService>();
        services.AddScoped<IPatientPackageCommandService, PatientPackageCommandService>();
        services.AddScoped<IPatientPackageQueryService, PatientPackageQueryService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<DiscountResolver>();
        services.AddScoped<IReportingQueryService, ReportingQueryService>();
        services.AddHostedService<SuspendedAppointmentRevalidationWorker>();
        services.AddScoped<DatabaseInitializer>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }

    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 4;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        options.User.RequireUniqueEmail = false;
    }
}
