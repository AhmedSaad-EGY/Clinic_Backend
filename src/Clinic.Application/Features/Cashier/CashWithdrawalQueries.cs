using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Cashier;

public sealed record GetCashWithdrawalQuery(long WithdrawalId,
    bool AdminOverride) : IQuery<CashWithdrawalModel>;

public sealed class GetCashWithdrawalQueryHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : IQueryHandler<GetCashWithdrawalQuery, CashWithdrawalModel>
{
    public Task<Result<CashWithdrawalModel>> Handle(GetCashWithdrawalQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure || query.WithdrawalId <= 0
            ? Task.FromResult(Result.Failure<CashWithdrawalModel>(actor.IsFailure
                ? actor.Error : CashierErrors.Validation(
                    "معرف طلب السحب غير صحيح.")))
            : service.GetAsync(actor.Value, query.WithdrawalId,
                query.AdminOverride, cancellationToken);
    }
}

public sealed record ListShiftCashWithdrawalsQuery(long ShiftId,
    bool AdminOverride, int PageNumber, int PageSize)
    : IQuery<CashWithdrawalPage>;

public sealed class ListShiftCashWithdrawalsQueryHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : IQueryHandler<ListShiftCashWithdrawalsQuery, CashWithdrawalPage>
{
    public Task<Result<CashWithdrawalPage>> Handle(
        ListShiftCashWithdrawalsQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result page = CashierValidation.Page(query.PageNumber, query.PageSize);
        return actor.IsFailure || page.IsFailure || query.ShiftId <= 0
            ? Task.FromResult(Result.Failure<CashWithdrawalPage>(actor.IsFailure
                ? actor.Error : page.IsFailure ? page.Error
                : CashierErrors.Validation("معرف الشيفت غير صحيح.")))
            : service.ListForShiftAsync(actor.Value, query.ShiftId,
                query.AdminOverride, query.PageNumber, query.PageSize,
                cancellationToken);
    }
}

public sealed record SearchCashWithdrawalsQuery(CashWithdrawalSearch Search)
    : IQuery<CashWithdrawalPage>;

public sealed class SearchCashWithdrawalsQueryHandler(ICurrentUser currentUser,
    ICashWithdrawalService service)
    : IQueryHandler<SearchCashWithdrawalsQuery, CashWithdrawalPage>
{
    public Task<Result<CashWithdrawalPage>> Handle(
        SearchCashWithdrawalsQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result validation = CashierValidation.CashWithdrawalSearch(query.Search);
        return actor.IsFailure || validation.IsFailure
            ? Task.FromResult(Result.Failure<CashWithdrawalPage>(actor.IsFailure
                ? actor.Error : validation.Error))
            : service.SearchAsync(query.Search, cancellationToken);
    }
}
