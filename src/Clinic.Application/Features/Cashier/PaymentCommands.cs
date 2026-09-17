namespace Clinic.Application.Features.Cashier;

public sealed record PostPaymentCommand(Guid IdempotencyKey, PostPaymentInput Input,
    bool AdminOverride) : ICommand<PostedPaymentModel>;

public sealed class PostPaymentCommandHandler(ICurrentUser currentUser,
    IPaymentService service) : ICommandHandler<PostPaymentCommand, PostedPaymentModel>
{
    public Task<Result<PostedPaymentModel>> Handle(PostPaymentCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result validation = CashierValidation.Payment(command.Input,
            command.IdempotencyKey, command.AdminOverride);
        return actor.IsFailure || validation.IsFailure
            ? Task.FromResult(Result.Failure<PostedPaymentModel>(
                actor.IsFailure ? actor.Error : validation.Error))
            : service.PostAsync(actor.Value, command.IdempotencyKey, command.Input,
                command.AdminOverride, cancellationToken);
    }
}
