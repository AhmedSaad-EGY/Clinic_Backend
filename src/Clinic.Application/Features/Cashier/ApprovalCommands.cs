namespace Clinic.Application.Features.Cashier;

public sealed record CreateCancellationApprovalCommand(long AppointmentId,
    string Reason, string AppointmentRowVersion) : ICommand<ApprovalRequestModel>;

public sealed class CreateCancellationApprovalCommandHandler(
    ICurrentUser currentUser, IApprovalRequestService service)
    : ICommandHandler<CreateCancellationApprovalCommand, ApprovalRequestModel>
{
    public Task<Result<ApprovalRequestModel>> Handle(
        CreateCancellationApprovalCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result reason = CashierValidation.Reason(command.Reason);
        Result<byte[]> version = CashierValidation.RowVersion(
            command.AppointmentRowVersion);
        if (actor.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<ApprovalRequestModel>(
                actor.IsFailure ? actor.Error : reason.IsFailure
                    ? reason.Error : version.Error));
        }

        return service.CreateCancellationAsync(actor.Value, command.AppointmentId,
            command.Reason, version.Value, cancellationToken);
    }
}

public sealed record ApproveApprovalRequestCommand(long RequestId, string Reason,
    string RowVersion) : ICommand<ApprovalRequestModel>;

public sealed class ApproveApprovalRequestCommandHandler(ICurrentUser currentUser,
    IApprovalRequestService service)
    : ICommandHandler<ApproveApprovalRequestCommand, ApprovalRequestModel>
{
    public Task<Result<ApprovalRequestModel>> Handle(
        ApproveApprovalRequestCommand command, CancellationToken cancellationToken) =>
        Review(currentUser, service, command.RequestId, command.Reason,
            command.RowVersion, approve: true, cancellationToken);

    private static async Task<Result<ApprovalRequestModel>> Review(
        ICurrentUser currentUser, IApprovalRequestService service, long requestId,
        string reason, string rowVersion, bool approve,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result reasonResult = CashierValidation.Reason(reason);
        Result<byte[]> version = CashierValidation.RowVersion(rowVersion);
        if (actor.IsFailure || reasonResult.IsFailure || version.IsFailure)
        {
            return Result.Failure<ApprovalRequestModel>(actor.IsFailure
                ? actor.Error : reasonResult.IsFailure
                    ? reasonResult.Error : version.Error);
        }

        return approve
            ? await service.ApproveAsync(actor.Value, requestId, reason,
                version.Value, cancellationToken)
            : await service.RejectAsync(actor.Value, requestId, reason,
                version.Value, cancellationToken);
    }

    internal static Task<Result<ApprovalRequestModel>> Reject(
        ICurrentUser currentUser, IApprovalRequestService service, long requestId,
        string reason, string rowVersion, CancellationToken cancellationToken) =>
        Review(currentUser, service, requestId, reason, rowVersion, approve: false,
            cancellationToken);
}

public sealed record RejectApprovalRequestCommand(long RequestId, string Reason,
    string RowVersion) : ICommand<ApprovalRequestModel>;

public sealed class RejectApprovalRequestCommandHandler(ICurrentUser currentUser,
    IApprovalRequestService service)
    : ICommandHandler<RejectApprovalRequestCommand, ApprovalRequestModel>
{
    public Task<Result<ApprovalRequestModel>> Handle(
        RejectApprovalRequestCommand command, CancellationToken cancellationToken) =>
        ApproveApprovalRequestCommandHandler.Reject(currentUser, service,
            command.RequestId, command.Reason, command.RowVersion,
            cancellationToken);
}

public sealed record ExecuteRefundCommand(Guid IdempotencyKey,
    ExecuteRefundInput Input) : ICommand<PostedRefundModel>;

public sealed class ExecuteRefundCommandHandler(ICurrentUser currentUser,
    IRefundService service) : ICommandHandler<ExecuteRefundCommand, PostedRefundModel>
{
    public Task<Result<PostedRefundModel>> Handle(ExecuteRefundCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result validation = CashierValidation.Refund(command.Input,
            command.IdempotencyKey);
        return actor.IsFailure || validation.IsFailure
            ? Task.FromResult(Result.Failure<PostedRefundModel>(actor.IsFailure
                ? actor.Error : validation.Error))
            : service.ExecuteAsync(actor.Value, command.IdempotencyKey,
                command.Input, cancellationToken);
    }
}
