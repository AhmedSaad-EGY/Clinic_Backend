namespace Clinic.Application.Features.Scheduling.Exceptions;

public sealed record CreateDoctorExceptionCommand(long DoctorId, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, DoctorExceptionType Type, string? Reason, bool ConfirmAffectedAppointments = false) : ICommand<DoctorExceptionModel>;
public sealed record CancelDoctorExceptionCommand(long ExceptionId, string RowVersion) : ICommand;

public sealed class CreateDoctorExceptionCommandHandler : ICommandHandler<CreateDoctorExceptionCommand, DoctorExceptionModel>
{
    private readonly ICurrentUser _user; private readonly IScheduleAdministrationService _service;
    public CreateDoctorExceptionCommandHandler(ICurrentUser user, IScheduleAdministrationService service) => (_user, _service) = (user, service);
    public Task<Result<DoctorExceptionModel>> Handle(CreateDoctorExceptionCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result reason = SchedulingValidation.Optional(command.Reason, "سبب الاستثناء", 500);
        Result exception = SchedulingValidation.DoctorException(
            command.StartTime,
            command.EndTime,
            command.Type);
        return actor.IsFailure || reason.IsFailure || exception.IsFailure
            ? Task.FromResult(Result.Failure<DoctorExceptionModel>(
                actor.IsFailure
                    ? actor.Error
                    : reason.IsFailure ? reason.Error : exception.Error))
            : _service.CreateExceptionAsync(actor.Value, command.DoctorId, command.Date, command.StartTime, command.EndTime, command.Type, command.Reason, command.ConfirmAffectedAppointments, cancellationToken);
    }
}

public sealed class CancelDoctorExceptionCommandHandler : ICommandHandler<CancelDoctorExceptionCommand>
{
    private readonly ICurrentUser _user; private readonly IScheduleAdministrationService _service;
    public CancelDoctorExceptionCommandHandler(ICurrentUser user, IScheduleAdministrationService service) => (_user, _service) = (user, service);
    public Task<Result> Handle(CancelDoctorExceptionCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user); Result<byte[]> version = SchedulingValidation.RowVersion(command.RowVersion);
        return actor.IsFailure || version.IsFailure ? Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error : version.Error)) : _service.CancelExceptionAsync(actor.Value, command.ExceptionId, version.Value, cancellationToken);
    }
}
