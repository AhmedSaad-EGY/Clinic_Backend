namespace Clinic.Application.Features.Cashier;

public sealed record GetShiftPolicyQuery : IQuery<ShiftPolicyModel>;

public sealed class GetShiftPolicyQueryHandler(ICurrentUser currentUser,
    IShiftService service) : IQueryHandler<GetShiftPolicyQuery, ShiftPolicyModel>
{
    public Task<Result<ShiftPolicyModel>> Handle(GetShiftPolicyQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<ShiftPolicyModel>(actor.Error))
            : service.GetPolicyAsync(cancellationToken);
    }
}

public sealed record ListCashDrawersQuery(int PageNumber, int PageSize)
    : IQuery<CashDrawerPage>;

public sealed class ListCashDrawersQueryHandler(ICurrentUser currentUser,
    IShiftService service) : IQueryHandler<ListCashDrawersQuery, CashDrawerPage>
{
    public Task<Result<CashDrawerPage>> Handle(ListCashDrawersQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result page = CashierValidation.Page(query.PageNumber, query.PageSize);
        return actor.IsFailure || page.IsFailure
            ? Task.FromResult(Result.Failure<CashDrawerPage>(
                actor.IsFailure ? actor.Error : page.Error))
            : service.ListDrawersAsync(query.PageNumber, query.PageSize,
                cancellationToken);
    }
}

public sealed record GetShiftQuery(long ShiftId) : IQuery<ShiftModel>;

public sealed class GetShiftQueryHandler(ICurrentUser currentUser,
    IShiftService service) : IQueryHandler<GetShiftQuery, ShiftModel>
{
    public Task<Result<ShiftModel>> Handle(GetShiftQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<ShiftModel>(actor.Error))
            : service.GetAsync(query.ShiftId, cancellationToken);
    }
}

public sealed record GetCurrentShiftQuery : IQuery<ShiftModel>;

public sealed class GetCurrentShiftQueryHandler(ICurrentUser currentUser,
    IShiftService service) : IQueryHandler<GetCurrentShiftQuery, ShiftModel>
{
    public Task<Result<ShiftModel>> Handle(GetCurrentShiftQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<ShiftModel>(actor.Error))
            : service.GetCurrentAsync(actor.Value, cancellationToken);
    }
}

public sealed record SearchShiftsQuery(ShiftSearch Search) : IQuery<ShiftPage>;

public sealed class SearchShiftsQueryHandler(ICurrentUser currentUser,
    IShiftService service) : IQueryHandler<SearchShiftsQuery, ShiftPage>
{
    public Task<Result<ShiftPage>> Handle(SearchShiftsQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result search = CashierValidation.Search(query.Search);
        return actor.IsFailure || search.IsFailure
            ? Task.FromResult(Result.Failure<ShiftPage>(
                actor.IsFailure ? actor.Error : search.Error))
            : service.SearchAsync(query.Search, cancellationToken);
    }
}

public sealed record GetShiftHistoryQuery(int PageNumber, int PageSize)
    : IQuery<ShiftPage>;

public sealed class GetShiftHistoryQueryHandler(ICurrentUser currentUser,
    IShiftService service) : IQueryHandler<GetShiftHistoryQuery, ShiftPage>
{
    public Task<Result<ShiftPage>> Handle(GetShiftHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result page = CashierValidation.Page(query.PageNumber, query.PageSize);
        return actor.IsFailure || page.IsFailure
            ? Task.FromResult(Result.Failure<ShiftPage>(
                actor.IsFailure ? actor.Error : page.Error))
            : service.HistoryAsync(actor.Value, query.PageNumber,
                query.PageSize, cancellationToken);
    }
}
