using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Cashier;

public sealed record CreateCashWithdrawalCommand(Guid IdempotencyKey,
    decimal Amount, string Reason) : ICommand<CreatedCashWithdrawalModel>;

public sealed class CreateCashWithdrawalCommandHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : ICommandHandler<CreateCashWithdrawalCommand, CreatedCashWithdrawalModel>
{
    public Task<Result<CreatedCashWithdrawalModel>> Handle(
        CreateCashWithdrawalCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result validation = CashierValidation.CashWithdrawal(command.Amount,
            command.Reason, command.IdempotencyKey);
        return actor.IsFailure || validation.IsFailure
            ? Task.FromResult(Result.Failure<CreatedCashWithdrawalModel>(
                actor.IsFailure ? actor.Error : validation.Error))
            : service.CreateAsync(actor.Value, command.IdempotencyKey,
                command.Amount, command.Reason, cancellationToken);
    }
}

public sealed record ApproveCashWithdrawalCommand(long WithdrawalId,
    string Reason, string RowVersion) : ICommand<CashWithdrawalModel>;

public sealed class ApproveCashWithdrawalCommandHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : ICommandHandler<ApproveCashWithdrawalCommand, CashWithdrawalModel>
{
    public Task<Result<CashWithdrawalModel>> Handle(
        ApproveCashWithdrawalCommand command, CancellationToken cancellationToken) =>
        Review(currentUser, service, command.WithdrawalId, command.Reason,
            command.RowVersion, approve: true, cancellationToken);

    internal static async Task<Result<CashWithdrawalModel>> Review(
        ICurrentUser currentUser, ICashWithdrawalService service,
        long withdrawalId, string reason, string rowVersion, bool approve,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result reasonResult = CashierValidation.Reason(reason);
        Result<byte[]> version = CashierValidation.RowVersion(rowVersion);
        if (actor.IsFailure || reasonResult.IsFailure || version.IsFailure ||
            withdrawalId <= 0)
        {
            return Result.Failure<CashWithdrawalModel>(actor.IsFailure
                ? actor.Error : reasonResult.IsFailure ? reasonResult.Error
                : version.IsFailure ? version.Error
                : CashierErrors.Validation("معرف طلب السحب غير صحيح."));
        }

        return approve
            ? await service.ApproveAsync(actor.Value, withdrawalId, reason,
                version.Value, cancellationToken)
            : await service.RejectAsync(actor.Value, withdrawalId, reason,
                version.Value, cancellationToken);
    }
}

public sealed record RejectCashWithdrawalCommand(long WithdrawalId,
    string Reason, string RowVersion) : ICommand<CashWithdrawalModel>;

public sealed class RejectCashWithdrawalCommandHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : ICommandHandler<RejectCashWithdrawalCommand, CashWithdrawalModel>
{
    public Task<Result<CashWithdrawalModel>> Handle(
        RejectCashWithdrawalCommand command, CancellationToken cancellationToken) =>
        ApproveCashWithdrawalCommandHandler.Review(currentUser, service,
            command.WithdrawalId, command.Reason, command.RowVersion,
            approve: false, cancellationToken);
}

public sealed record CancelCashWithdrawalCommand(long WithdrawalId,
    string RowVersion) : ICommand<CashWithdrawalModel>;

public sealed class CancelCashWithdrawalCommandHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : ICommandHandler<CancelCashWithdrawalCommand, CashWithdrawalModel>
{
    public Task<Result<CashWithdrawalModel>> Handle(
        CancelCashWithdrawalCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        return actor.IsFailure || version.IsFailure || command.WithdrawalId <= 0
            ? Task.FromResult(Result.Failure<CashWithdrawalModel>(actor.IsFailure
                ? actor.Error : version.IsFailure ? version.Error
                : CashierErrors.Validation("معرف طلب السحب غير صحيح.")))
            : service.CancelAsync(actor.Value, command.WithdrawalId,
                version.Value, cancellationToken);
    }
}

public sealed record ExecuteCashWithdrawalCommand(long WithdrawalId,
    Guid IdempotencyKey, string RowVersion)
    : ICommand<ExecutedCashWithdrawalModel>;

public sealed class ExecuteCashWithdrawalCommandHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : ICommandHandler<ExecuteCashWithdrawalCommand, ExecutedCashWithdrawalModel>
{
    public Task<Result<ExecutedCashWithdrawalModel>> Handle(
        ExecuteCashWithdrawalCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result<byte[]> version = CashierValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.WithdrawalId <= 0 ||
            command.IdempotencyKey == Guid.Empty)
        {
            return Task.FromResult(Result.Failure<ExecutedCashWithdrawalModel>(
                actor.IsFailure ? actor.Error : version.IsFailure ? version.Error
                : CashierErrors.Validation("بيانات تنفيذ السحب غير صحيحة.")));
        }

        return service.ExecuteAsync(actor.Value, command.WithdrawalId,
            command.IdempotencyKey, version.Value, cancellationToken);
    }
}
