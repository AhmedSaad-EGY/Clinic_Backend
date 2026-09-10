using Clinic.Application.Abstractions.Discounts;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Discounts;

public sealed record GetDiscountQuery(long DiscountId) : IQuery<DiscountModel>;
public sealed record SearchDiscountsQuery(DiscountFilter Filter) : IQuery<DiscountPage>;

public sealed class GetDiscountQueryHandler(IDiscountService service)
    : IQueryHandler<GetDiscountQuery, DiscountModel>
{
    public Task<Result<DiscountModel>> Handle(GetDiscountQuery query,
        CancellationToken cancellationToken) => query.DiscountId <= 0
        ? Task.FromResult(Result.Failure<DiscountModel>(
            DiscountErrors.Validation("رقم الخصم غير صحيح.")))
        : service.GetAsync(query.DiscountId, cancellationToken);
}

public sealed class SearchDiscountsQueryHandler(IDiscountService service)
    : IQueryHandler<SearchDiscountsQuery, DiscountPage>
{
    public Task<Result<DiscountPage>> Handle(SearchDiscountsQuery query,
        CancellationToken cancellationToken)
    {
        DiscountFilter filter = query.Filter;
        if (filter.PageNumber < 1 || filter.PageSize is < 1 or > 100 ||
            filter.DepartmentId is <= 0 || filter.Type.HasValue && !Enum.IsDefined(filter.Type.Value) ||
            filter.AppliesTo.HasValue && !Enum.IsDefined(filter.AppliesTo.Value))
        {
            return Task.FromResult(Result.Failure<DiscountPage>(DiscountErrors.Validation(
                "فلاتر البحث أو بيانات الصفحة غير صحيحة.")));
        }
        return service.SearchAsync(filter, cancellationToken);
    }
}
