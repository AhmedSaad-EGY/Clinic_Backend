namespace Clinic.Application.Features.Cashier;

public sealed record UpdateShiftPolicyCommand(int ClosingGraceMinutes,
    string RowVersion) : ICommand<ShiftPolicyModel>;

public sealed class UpdateShiftPolicyCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<UpdateShiftPolicyCommand, ShiftPolicyModel>
{
    public Task<Result<ShiftPolicyModel>> Handle(UpdateShiftPolicyCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure ||
            command.ClosingGraceMinutes is < 0 or > 120)
        {
            return Task.FromResult(Result.Failure<ShiftPolicyModel>(actor.IsFailure
                ? actor.Error : version.IsFailure ? version.Error
                : CashierErrors.Validation("مهلة الإغلاق يجب أن تكون بين صفر و120 دقيقة.")));
        }

        return service.UpdatePolicyAsync(actor.Value, command.ClosingGraceMinutes,
            version.Value, cancellationToken);
    }
}

public sealed record GenerateShiftsCommand(GenerateShiftsInput Input)
    : ICommand<GeneratedShifts>;

public sealed class GenerateShiftsCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<GenerateShiftsCommand, GeneratedShifts>
{
    public Task<Result<GeneratedShifts>> Handle(GenerateShiftsCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result input = CashierValidation.Generation(command.Input);
        return actor.IsFailure || input.IsFailure
            ? Task.FromResult(Result.Failure<GeneratedShifts>(
                actor.IsFailure ? actor.Error : input.Error))
            : service.GenerateAsync(actor.Value, command.Input, cancellationToken);
    }
}

public sealed record UpdateShiftCommand(long ShiftId, UpdateShiftInput Input,
    string RowVersion) : ICommand<ShiftModel>;

public sealed class UpdateShiftCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<UpdateShiftCommand, ShiftModel>
{
    public Task<Result<ShiftModel>> Handle(UpdateShiftCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result input = CashierValidation.Update(command.Input);
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || input.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<ShiftModel>(actor.IsFailure
                ? actor.Error : input.IsFailure ? input.Error : version.Error));
        }

        return service.UpdateAsync(actor.Value, command.ShiftId, command.Input,
            version.Value, cancellationToken);
    }
}

public sealed record ExtendShiftCommand(long ShiftId, DateTimeOffset NewScheduledEnd,
    string Reason, string RowVersion) : ICommand<ShiftModel>;

public sealed class ExtendShiftCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<ExtendShiftCommand, ShiftModel>
{
    public Task<Result<ShiftModel>> Handle(ExtendShiftCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result reason = CashierValidation.Reason(command.Reason);
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<ShiftModel>(actor.IsFailure
                ? actor.Error : reason.IsFailure ? reason.Error : version.Error));
        }

        return service.ExtendAsync(actor.Value, command.ShiftId,
            command.NewScheduledEnd, command.Reason.Trim(), version.Value,
            cancellationToken);
    }
}

public sealed record CancelShiftCommand(long ShiftId, string Reason,
    string RowVersion) : ICommand;

public sealed class CancelShiftCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<CancelShiftCommand>
{
    public Task<Result> Handle(CancelShiftCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result reason = CashierValidation.Reason(command.Reason);
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure(actor.IsFailure
                ? actor.Error : reason.IsFailure ? reason.Error : version.Error));
        }

        return service.CancelAsync(actor.Value, command.ShiftId,
            command.Reason.Trim(), version.Value, cancellationToken);
    }
}

public sealed record RecordOpeningBalanceCommand(long ShiftId, decimal Amount,
    bool AdminOverride, string? Reason, string RowVersion) : ICommand<ShiftModel>;

public sealed class RecordOpeningBalanceCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<RecordOpeningBalanceCommand, ShiftModel>
{
    public Task<Result<ShiftModel>> Handle(RecordOpeningBalanceCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result money = CashierValidation.Money(command.Amount);
        Result reason = command.AdminOverride
            ? CashierValidation.Reason(command.Reason)
            : Result.Success();
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || money.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<ShiftModel>(actor.IsFailure
                ? actor.Error : money.IsFailure ? money.Error
                : reason.IsFailure ? reason.Error : version.Error));
        }

        return service.RecordOpeningBalanceAsync(actor.Value, command.ShiftId,
            command.Amount, command.AdminOverride, command.Reason?.Trim(),
            version.Value, cancellationToken);
    }
}

public sealed record ReconcileShiftCommand(long ShiftId, decimal DeclaredCash,
    bool AdminOverride, string? Reason, string RowVersion) : ICommand<ShiftModel>;

public sealed class ReconcileShiftCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<ReconcileShiftCommand, ShiftModel>
{
    public Task<Result<ShiftModel>> Handle(ReconcileShiftCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result money = CashierValidation.Money(command.DeclaredCash);
        Result reason = command.AdminOverride
            ? CashierValidation.Reason(command.Reason)
            : Result.Success();
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || money.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<ShiftModel>(actor.IsFailure
                ? actor.Error : money.IsFailure ? money.Error
                : reason.IsFailure ? reason.Error : version.Error));
        }

        return service.ReconcileAsync(actor.Value, command.ShiftId,
            command.DeclaredCash, command.AdminOverride, command.Reason?.Trim(),
            version.Value, cancellationToken);
    }
}

public sealed record CloseShiftCommand(long ShiftId, bool AdminOverride,
    string? Reason, string RowVersion) : ICommand<ShiftModel>;

public sealed class CloseShiftCommandHandler(ICurrentUser currentUser,
    IShiftService service) : ICommandHandler<CloseShiftCommand, ShiftModel>
{
    public Task<Result<ShiftModel>> Handle(CloseShiftCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result reason = command.AdminOverride
            ? CashierValidation.Reason(command.Reason)
            : Result.Success();
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<ShiftModel>(actor.IsFailure
                ? actor.Error : reason.IsFailure ? reason.Error : version.Error));
        }

        return service.CloseAsync(actor.Value, command.ShiftId,
            command.AdminOverride, command.Reason?.Trim(), version.Value,
            cancellationToken);
    }
}
