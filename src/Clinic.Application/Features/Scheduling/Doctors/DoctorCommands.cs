namespace Clinic.Application.Features.Scheduling.Doctors;

public sealed record CreateDoctorCommand(long DepartmentId, string Name, string? Phone) : ICommand<DoctorModel>;
public sealed record UpdateDoctorCommand(long DoctorId, string Name, string? Phone, bool IsActive, string RowVersion) : ICommand<DoctorModel>;
public sealed record ReplaceDoctorServicesCommand(long DoctorId, IReadOnlyCollection<long> ServiceIds, string RowVersion) : ICommand<DoctorModel>;
public sealed record ArchiveDoctorCommand(long DoctorId, string RowVersion) : ICommand;

public sealed class CreateDoctorCommandHandler : ICommandHandler<CreateDoctorCommand, DoctorModel>
{
    private readonly ICurrentUser _user;
    private readonly IDoctorAdministrationService _service;
    public CreateDoctorCommandHandler(ICurrentUser user, IDoctorAdministrationService service) => (_user, _service) = (user, service);

    public Task<Result<DoctorModel>> Handle(CreateDoctorCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result name = SchedulingValidation.Required(command.Name, "اسم الطبيب", 150);
        Result phone = SchedulingValidation.Optional(command.Phone, "رقم الهاتف", 30);
        if (actor.IsFailure || name.IsFailure || phone.IsFailure || command.DepartmentId <= 0)
        {
            ResultError error = actor.IsFailure ? actor.Error : name.IsFailure ? name.Error : phone.IsFailure ? phone.Error : SchedulingErrors.Validation("القسم غير صحيح.");
            return Task.FromResult(Result.Failure<DoctorModel>(error));
        }

        return _service.CreateAsync(actor.Value, command.DepartmentId, command.Name.Trim(), string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim(), cancellationToken);
    }
}

public sealed class UpdateDoctorCommandHandler : ICommandHandler<UpdateDoctorCommand, DoctorModel>
{
    private readonly ICurrentUser _user;
    private readonly IDoctorAdministrationService _service;
    public UpdateDoctorCommandHandler(ICurrentUser user, IDoctorAdministrationService service) => (_user, _service) = (user, service);

    public Task<Result<DoctorModel>> Handle(UpdateDoctorCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result<byte[]> version = SchedulingValidation.RowVersion(command.RowVersion);
        Result name = SchedulingValidation.Required(command.Name, "اسم الطبيب", 150);
        Result phone = SchedulingValidation.Optional(command.Phone, "رقم الهاتف", 30);
        if (actor.IsFailure || version.IsFailure || name.IsFailure || phone.IsFailure)
        {
            ResultError error = actor.IsFailure ? actor.Error : version.IsFailure ? version.Error : name.IsFailure ? name.Error : phone.Error;
            return Task.FromResult(Result.Failure<DoctorModel>(error));
        }

        return _service.UpdateAsync(actor.Value, command.DoctorId, command.Name.Trim(), string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim(), command.IsActive, version.Value, cancellationToken);
    }
}

public sealed class ReplaceDoctorServicesCommandHandler : ICommandHandler<ReplaceDoctorServicesCommand, DoctorModel>
{
    private readonly ICurrentUser _user;
    private readonly IDoctorAdministrationService _service;
    public ReplaceDoctorServicesCommandHandler(ICurrentUser user, IDoctorAdministrationService service) => (_user, _service) = (user, service);

    public Task<Result<DoctorModel>> Handle(ReplaceDoctorServicesCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result<byte[]> version = SchedulingValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<DoctorModel>(actor.IsFailure ? actor.Error : version.Error));
        }

        return _service.ReplaceServicesAsync(actor.Value, command.DoctorId, command.ServiceIds, version.Value, cancellationToken);
    }
}

public sealed class ArchiveDoctorCommandHandler : ICommandHandler<ArchiveDoctorCommand>
{
    private readonly ICurrentUser _user;
    private readonly IDoctorAdministrationService _service;
    public ArchiveDoctorCommandHandler(ICurrentUser user, IDoctorAdministrationService service) => (_user, _service) = (user, service);

    public Task<Result> Handle(ArchiveDoctorCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result<byte[]> version = SchedulingValidation.RowVersion(command.RowVersion);
        return actor.IsFailure || version.IsFailure
            ? Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error : version.Error))
            : _service.ArchiveAsync(actor.Value, command.DoctorId, version.Value, cancellationToken);
    }
}
