namespace Clinic.Application.Features.Cashier;

public sealed record ListPaymentMethodsQuery
    : IQuery<IReadOnlyCollection<PaymentMethodModel>>;

public sealed class ListPaymentMethodsQueryHandler(ICurrentUser currentUser,
    IPaymentService service)
    : IQueryHandler<ListPaymentMethodsQuery, IReadOnlyCollection<PaymentMethodModel>>
{
    public Task<Result<IReadOnlyCollection<PaymentMethodModel>>> Handle(
        ListPaymentMethodsQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<IReadOnlyCollection<PaymentMethodModel>>(
                actor.Error))
            : service.ListMethodsAsync(cancellationToken);
    }
}

public sealed record GetPaymentQuery(long PaymentId, bool AdminOverride)
    : IQuery<PaymentModel>;

public sealed class GetPaymentQueryHandler(ICurrentUser currentUser,
    IPaymentService service) : IQueryHandler<GetPaymentQuery, PaymentModel>
{
    public Task<Result<PaymentModel>> Handle(GetPaymentQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<PaymentModel>(actor.Error))
            : service.GetAsync(actor.Value, query.PaymentId, query.AdminOverride,
                cancellationToken);
    }
}


public sealed record ListShiftPaymentsQuery(long ShiftId, bool AdminOverride,
    int PageNumber, int PageSize) : IQuery<PaymentPage>;

public sealed class ListShiftPaymentsQueryHandler(ICurrentUser currentUser,
    IPaymentService service) : IQueryHandler<ListShiftPaymentsQuery, PaymentPage>
{
    public Task<Result<PaymentPage>> Handle(ListShiftPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        Result page = CashierValidation.Page(query.PageNumber, query.PageSize);
        return actor.IsFailure || page.IsFailure
            ? Task.FromResult(Result.Failure<PaymentPage>(
                actor.IsFailure ? actor.Error : page.Error))
            : service.ListForShiftAsync(actor.Value, query.ShiftId,
                query.AdminOverride, query.PageNumber, query.PageSize,
                cancellationToken);
    }
}

public sealed record GetShiftCollectionSummaryQuery(long ShiftId,
    bool AdminOverride) : IQuery<ShiftCollectionSummaryModel>;

public sealed class GetShiftCollectionSummaryQueryHandler(ICurrentUser currentUser,
    IPaymentService service)
    : IQueryHandler<GetShiftCollectionSummaryQuery, ShiftCollectionSummaryModel>
{
    public Task<Result<ShiftCollectionSummaryModel>> Handle(
        GetShiftCollectionSummaryQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = CashierValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<ShiftCollectionSummaryModel>(actor.Error))
            : service.GetShiftSummaryAsync(actor.Value, query.ShiftId,
                query.AdminOverride, cancellationToken);
    }
}
