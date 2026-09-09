using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Appointments;

public sealed record CheckAppointmentAvailabilityQuery(AppointmentInput Input,
    long? ExcludedAppointmentId = null) : IQuery<AppointmentAvailability>;

public sealed class CheckAppointmentAvailabilityQueryHandler(
    ICurrentUser currentUser, IAppointmentService service)
    : IQueryHandler<CheckAppointmentAvailabilityQuery, AppointmentAvailability>
{
    public Task<Result<AppointmentAvailability>> Handle(CheckAppointmentAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        Result input = AppointmentValidation.Input(query.Input);
        return actor.IsFailure || input.IsFailure
            ? Task.FromResult(Result.Failure<AppointmentAvailability>(
                actor.IsFailure ? actor.Error : input.Error))
            : service.CheckAvailabilityAsync(actor.Value, query.Input,
                query.ExcludedAppointmentId,
                cancellationToken);
    }
}

public sealed record CreateAppointmentCommand(AppointmentInput Input) : ICommand<AppointmentModel>;

public sealed class CreateAppointmentCommandHandler(ICurrentUser currentUser,
    IAppointmentService service) : ICommandHandler<CreateAppointmentCommand, AppointmentModel>
{
    public Task<Result<AppointmentModel>> Handle(CreateAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        Result input = AppointmentValidation.Input(command.Input);
        return actor.IsFailure || input.IsFailure
            ? Task.FromResult(Result.Failure<AppointmentModel>(
                actor.IsFailure ? actor.Error : input.Error))
            : service.CreateAsync(actor.Value, command.Input, cancellationToken);
    }
}

public sealed record UpdateAppointmentCommand(long AppointmentId, AppointmentInput Input,
    string RowVersion) : ICommand<AppointmentModel>;

public sealed class UpdateAppointmentCommandHandler(ICurrentUser currentUser,
    IAppointmentService service) : ICommandHandler<UpdateAppointmentCommand, AppointmentModel>
{
    public Task<Result<AppointmentModel>> Handle(UpdateAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        Result input = AppointmentValidation.Input(command.Input);
        Result<byte[]> version = AppointmentValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || input.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<AppointmentModel>(actor.IsFailure
                ? actor.Error : input.IsFailure ? input.Error : version.Error));
        }

        return service.UpdateAsync(actor.Value, command.AppointmentId, command.Input,
            version.Value, cancellationToken);
    }
}

public sealed record ChangeAppointmentStateCommand(long AppointmentId,
    AppointmentStateAction Action, string? Reason, string RowVersion) : ICommand;

public enum AppointmentStateAction { Cancel = 1, Complete = 2, NoShow = 3 }

public sealed class ChangeAppointmentStateCommandHandler(ICurrentUser currentUser,
    IAppointmentService service) : ICommandHandler<ChangeAppointmentStateCommand>
{
    public Task<Result> Handle(ChangeAppointmentStateCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        Result<byte[]> version = AppointmentValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error : version.Error));
        }

        if (!Enum.IsDefined(command.Action) || command.Reason?.Length > 500)
        {
            return Task.FromResult(Result.Failure(
                AppointmentErrors.Validation("طلب تغيير حالة الحجز غير صحيح.")));
        }

        return command.Action switch
        {
            AppointmentStateAction.Cancel => service.CancelAsync(actor.Value,
                command.AppointmentId, command.Reason, version.Value, cancellationToken),
            AppointmentStateAction.Complete => service.CompleteAsync(actor.Value,
                command.AppointmentId, version.Value, cancellationToken),
            AppointmentStateAction.NoShow => service.MarkNoShowAsync(actor.Value,
                command.AppointmentId, version.Value, cancellationToken),
            _ => Task.FromResult(Result.Failure(
                AppointmentErrors.Validation("إجراء الحجز غير صحيح.")))
        };
    }
}

public sealed record RevalidateSuspendedAppointmentsCommand(long? DepartmentId) : ICommand<int>;

public sealed class RevalidateSuspendedAppointmentsCommandHandler(ICurrentUser currentUser,
    IAppointmentService service)
    : ICommandHandler<RevalidateSuspendedAppointmentsCommand, int>
{
    public Task<Result<int>> Handle(RevalidateSuspendedAppointmentsCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<int>(actor.Error))
            : service.RevalidateSuspendedAsync(actor.Value, command.DepartmentId,
                cancellationToken);
    }
}

public sealed record TransferAppointmentDoctorCommand(long AppointmentId,
    long AppointmentServiceId, long DoctorId, string Reason, string RowVersion)
    : ICommand<AppointmentModel>;

public sealed class TransferAppointmentDoctorCommandHandler(ICurrentUser currentUser,
    IAppointmentService service)
    : ICommandHandler<TransferAppointmentDoctorCommand, AppointmentModel>
{
    public Task<Result<AppointmentModel>> Handle(TransferAppointmentDoctorCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        Result<byte[]> version = AppointmentValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.DoctorId <= 0 ||
            string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500)
        {
            return Task.FromResult(Result.Failure<AppointmentModel>(actor.IsFailure
                ? actor.Error : version.IsFailure ? version.Error
                : AppointmentErrors.Validation("سبب النقل والطبيب الجديد مطلوبان.")));
        }

        return service.TransferDoctorAsync(actor.Value, command.AppointmentId,
            command.AppointmentServiceId, command.DoctorId, command.Reason.Trim(),
            version.Value, cancellationToken);
    }
}
