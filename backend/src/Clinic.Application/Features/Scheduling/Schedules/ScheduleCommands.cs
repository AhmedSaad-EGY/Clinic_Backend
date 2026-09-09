using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Scheduling;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using Clinic.Domain.Scheduling;

namespace Clinic.Application.Features.Scheduling.Schedules;

public sealed record CreateDoctorScheduleCommand(long DoctorId, ClinicDayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, DateOnly EffectiveFrom, DateOnly? EffectiveTo) : ICommand<DoctorScheduleModel>;
public sealed record UpdateDoctorScheduleCommand(long ScheduleId, ClinicDayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string RowVersion, bool ConfirmAffectedAppointments = false) : ICommand<DoctorScheduleModel>;
public sealed record DeactivateDoctorScheduleCommand(long ScheduleId, string RowVersion, bool ConfirmAffectedAppointments = false) : ICommand;

public sealed class CreateDoctorScheduleCommandHandler : ICommandHandler<CreateDoctorScheduleCommand, DoctorScheduleModel>
{
    private readonly ICurrentUser _user; private readonly IScheduleAdministrationService _service;
    public CreateDoctorScheduleCommandHandler(ICurrentUser user, IScheduleAdministrationService service) => (_user, _service) = (user, service);
    public Task<Result<DoctorScheduleModel>> Handle(CreateDoctorScheduleCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result schedule = SchedulingValidation.Schedule(
            command.DayOfWeek,
            command.StartTime,
            command.EndTime,
            command.EffectiveFrom,
            command.EffectiveTo);
        return actor.IsFailure || schedule.IsFailure
            ? Task.FromResult(Result.Failure<DoctorScheduleModel>(
                actor.IsFailure ? actor.Error : schedule.Error))
            : _service.CreateScheduleAsync(actor.Value, command.DoctorId, command.DayOfWeek, command.StartTime, command.EndTime, command.EffectiveFrom, command.EffectiveTo, cancellationToken);
    }
}

public sealed class UpdateDoctorScheduleCommandHandler : ICommandHandler<UpdateDoctorScheduleCommand, DoctorScheduleModel>
{
    private readonly ICurrentUser _user; private readonly IScheduleAdministrationService _service;
    public UpdateDoctorScheduleCommandHandler(ICurrentUser user, IScheduleAdministrationService service) => (_user, _service) = (user, service);
    public Task<Result<DoctorScheduleModel>> Handle(UpdateDoctorScheduleCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result<byte[]> version = SchedulingValidation.RowVersion(command.RowVersion);
        Result schedule = SchedulingValidation.Schedule(
            command.DayOfWeek,
            command.StartTime,
            command.EndTime,
            command.EffectiveFrom,
            command.EffectiveTo);
        return actor.IsFailure || version.IsFailure || schedule.IsFailure
            ? Task.FromResult(Result.Failure<DoctorScheduleModel>(
                actor.IsFailure
                    ? actor.Error
                    : version.IsFailure ? version.Error : schedule.Error))
            : _service.UpdateScheduleAsync(actor.Value, command.ScheduleId, command.DayOfWeek, command.StartTime, command.EndTime, command.EffectiveFrom, command.EffectiveTo, version.Value, command.ConfirmAffectedAppointments, cancellationToken);
    }
}

public sealed class DeactivateDoctorScheduleCommandHandler : ICommandHandler<DeactivateDoctorScheduleCommand>
{
    private readonly ICurrentUser _user; private readonly IScheduleAdministrationService _service;
    public DeactivateDoctorScheduleCommandHandler(ICurrentUser user, IScheduleAdministrationService service) => (_user, _service) = (user, service);
    public Task<Result> Handle(DeactivateDoctorScheduleCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user); Result<byte[]> version = SchedulingValidation.RowVersion(command.RowVersion);
        return actor.IsFailure || version.IsFailure ? Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error : version.Error)) : _service.DeactivateScheduleAsync(actor.Value, command.ScheduleId, version.Value, command.ConfirmAffectedAppointments, cancellationToken);
    }
}
