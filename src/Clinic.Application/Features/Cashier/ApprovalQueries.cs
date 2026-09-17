namespace Clinic.Application.Features.Cashier;

public sealed record GetApprovalRequestQuery(long RequestId)
    : IQuery<ApprovalRequestModel>;

public sealed class GetApprovalRequestQueryHandler(ICurrentUser currentUser,
    IApprovalRequestService service)
    : IQueryHandler<GetApprovalRequestQuery, ApprovalRequestModel>
{
    public Task<Result<ApprovalRequestModel>> Handle(GetApprovalRequestQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<ApprovalRequestModel>(actor.Error))
            : service.GetAsync(query.RequestId, cancellationToken);
    }
}

public sealed record SearchApprovalRequestsQuery(ApprovalRequestSearch Search)
    : IQuery<ApprovalRequestPage>;

public sealed class SearchApprovalRequestsQueryHandler(ICurrentUser currentUser,
    IApprovalRequestService service)
    : IQueryHandler<SearchApprovalRequestsQuery, ApprovalRequestPage>
{
    public Task<Result<ApprovalRequestPage>> Handle(
        SearchApprovalRequestsQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result validation = CashierValidation.ApprovalSearch(query.Search);
        return actor.IsFailure || validation.IsFailure
            ? Task.FromResult(Result.Failure<ApprovalRequestPage>(actor.IsFailure
                ? actor.Error : validation.Error))
            : service.SearchAsync(query.Search, cancellationToken);
    }
}

public sealed record GetRefundQuery(long RefundId, bool AdminOverride)
    : IQuery<RefundModel>;

public sealed class GetRefundQueryHandler(ICurrentUser currentUser,
    IRefundService service) : IQueryHandler<GetRefundQuery, RefundModel>
{
    public Task<Result<RefundModel>> Handle(GetRefundQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<RefundModel>(actor.Error))
            : service.GetAsync(actor.Value, query.RefundId, query.AdminOverride,
                cancellationToken);
    }
}

public sealed record ListShiftRefundsQuery(long ShiftId, bool AdminOverride,
    int PageNumber, int PageSize) : IQuery<RefundPage>;

public sealed class ListShiftRefundsQueryHandler(ICurrentUser currentUser,
    IRefundService service) : IQueryHandler<ListShiftRefundsQuery, RefundPage>
{
    public Task<Result<RefundPage>> Handle(ListShiftRefundsQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result page = CashierValidation.Page(query.PageNumber, query.PageSize);
        return actor.IsFailure || page.IsFailure
            ? Task.FromResult(Result.Failure<RefundPage>(actor.IsFailure
                ? actor.Error : page.Error))
            : service.ListForShiftAsync(actor.Value, query.ShiftId,
                query.AdminOverride, query.PageNumber, query.PageSize,
                cancellationToken);
    }
}
