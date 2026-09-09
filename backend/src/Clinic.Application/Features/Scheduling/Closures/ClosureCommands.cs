using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Scheduling;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Scheduling.Closures;

public sealed record CreateDepartmentClosureCommand(long DepartmentId, DateTimeOffset StartAt, DateTimeOffset EndAt, string Reason, bool ConfirmAffectedAppointments = false) : ICommand<DepartmentClosureModel>;
public sealed record CancelDepartmentClosureCommand(long ClosureId, string RowVersion) : ICommand;

public sealed class CreateDepartmentClosureCommandHandler : ICommandHandler<CreateDepartmentClosureCommand, DepartmentClosureModel>
{
    private readonly ICurrentUser _user; private readonly IScheduleAdministrationService _service;
    public CreateDepartmentClosureCommandHandler(ICurrentUser user, IScheduleAdministrationService service) => (_user, _service) = (user, service);
    public Task<Result<DepartmentClosureModel>> Handle(CreateDepartmentClosureCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user);
        Result reason = SchedulingValidation.Required(command.Reason, "سبب إيقاف القسم", 500);
        Result closure = SchedulingValidation.Closure(command.StartAt, command.EndAt);
        return actor.IsFailure || reason.IsFailure || closure.IsFailure
            ? Task.FromResult(Result.Failure<DepartmentClosureModel>(
                actor.IsFailure
                    ? actor.Error
                    : reason.IsFailure ? reason.Error : closure.Error))
            : _service.CreateClosureAsync(actor.Value, command.DepartmentId, command.StartAt, command.EndAt, command.Reason.Trim(), command.ConfirmAffectedAppointments, cancellationToken);
    }
}

public sealed class CancelDepartmentClosureCommandHandler : ICommandHandler<CancelDepartmentClosureCommand>
{
    private readonly ICurrentUser _user; private readonly IScheduleAdministrationService _service;
    public CancelDepartmentClosureCommandHandler(ICurrentUser user, IScheduleAdministrationService service) => (_user, _service) = (user, service);
    public Task<Result> Handle(CancelDepartmentClosureCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = SchedulingValidation.Actor(_user); Result<byte[]> version = SchedulingValidation.RowVersion(command.RowVersion);
        return actor.IsFailure || version.IsFailure ? Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error : version.Error)) : _service.CancelClosureAsync(actor.Value, command.ClosureId, version.Value, cancellationToken);
    }
}
