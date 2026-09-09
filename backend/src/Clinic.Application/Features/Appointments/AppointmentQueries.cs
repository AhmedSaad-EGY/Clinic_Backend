using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Appointments;

public sealed record GetAppointmentQuery(long AppointmentId) : IQuery<AppointmentModel>;

public sealed class GetAppointmentQueryHandler(ICurrentUser currentUser,
    IAppointmentService service) : IQueryHandler<GetAppointmentQuery, AppointmentModel>
{
    public Task<Result<AppointmentModel>> Handle(GetAppointmentQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<AppointmentModel>(actor.Error))
            : service.GetAsync(query.AppointmentId, cancellationToken);
    }
}

public sealed record GetAppointmentCalendarQuery(DateOnly Date, long? DepartmentId)
    : IQuery<IReadOnlyCollection<AppointmentModel>>;

public sealed class GetAppointmentCalendarQueryHandler(ICurrentUser currentUser,
    IAppointmentService service)
    : IQueryHandler<GetAppointmentCalendarQuery, IReadOnlyCollection<AppointmentModel>>
{
    public Task<Result<IReadOnlyCollection<AppointmentModel>>> Handle(
        GetAppointmentCalendarQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<IReadOnlyCollection<AppointmentModel>>(actor.Error))
            : service.CalendarAsync(query.Date, query.DepartmentId, cancellationToken);
    }
}

public sealed record SearchAppointmentsQuery(AppointmentSearch Search)
    : IQuery<AppointmentPage>;

public sealed class SearchAppointmentsQueryHandler(ICurrentUser currentUser,
    IAppointmentService service) : IQueryHandler<SearchAppointmentsQuery, AppointmentPage>
{
    public Task<Result<AppointmentPage>> Handle(SearchAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = AppointmentValidation.Actor(currentUser);
        if (actor.IsFailure)
        {
            return Task.FromResult(Result.Failure<AppointmentPage>(actor.Error));
        }

        AppointmentSearch search = query.Search;
        if (search.PageNumber <= 0 || search.PageSize is <= 0 or > 100 ||
            search.From > search.To || search.MinimumAge is < 0 or > 130 ||
            search.MaximumAge is < 0 or > 130 || search.MinimumAge > search.MaximumAge)
        {
            return Task.FromResult(Result.Failure<AppointmentPage>(
                AppointmentErrors.Validation("معايير البحث غير صحيحة.")));
        }

        return service.SearchAsync(search, cancellationToken);
    }
}
