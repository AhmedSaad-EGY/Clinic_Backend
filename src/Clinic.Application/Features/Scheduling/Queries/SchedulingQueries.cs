using Clinic.Application.Abstractions.Scheduling;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.Scheduling.Queries;

public sealed record ListDoctorsQuery(
    long? DepartmentId,
    long? ServiceId,
    bool IncludeArchived,
    int PageNumber,
    int PageSize) : IQuery<SchedulingPage<DoctorModel>>;

public sealed record GetDoctorQuery(long DoctorId, bool IncludeArchived) : IQuery<DoctorModel>;

public sealed record ListDoctorSchedulesQuery(
    long DoctorId,
    DateOnly? From,
    DateOnly? To,
    int PageNumber,
    int PageSize) : IQuery<SchedulingPage<DoctorScheduleModel>>;

public sealed record ListDoctorExceptionsQuery(
    long DoctorId,
    DateOnly From,
    DateOnly To,
    bool IncludeCancelled,
    int PageNumber,
    int PageSize) : IQuery<SchedulingPage<DoctorExceptionModel>>;

public sealed record GetDepartmentAvailabilityQuery(
    long DepartmentId,
    DateTimeOffset At) : IQuery<DepartmentAvailabilityModel>;

public sealed record ListDepartmentClosuresQuery(
    long DepartmentId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    bool IncludeCancelled,
    int PageNumber,
    int PageSize) : IQuery<SchedulingPage<DepartmentClosureModel>>;

public sealed class ListDoctorsQueryHandler(ISchedulingQueryService service)
    : IQueryHandler<ListDoctorsQuery, SchedulingPage<DoctorModel>>
{
    public Task<Result<SchedulingPage<DoctorModel>>> Handle(
        ListDoctorsQuery query,
        CancellationToken cancellationToken)
    {
        Result page = SchedulingValidation.Page(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<SchedulingPage<DoctorModel>>(page.Error))
            : service.ListDoctorsAsync(
                query.DepartmentId,
                query.ServiceId,
                query.IncludeArchived,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}

public sealed class GetDoctorQueryHandler(ISchedulingQueryService service)
    : IQueryHandler<GetDoctorQuery, DoctorModel>
{
    public Task<Result<DoctorModel>> Handle(
        GetDoctorQuery query,
        CancellationToken cancellationToken) =>
        service.GetDoctorAsync(query.DoctorId, query.IncludeArchived, cancellationToken);
}

public sealed class ListDoctorSchedulesQueryHandler(ISchedulingQueryService service)
    : IQueryHandler<ListDoctorSchedulesQuery, SchedulingPage<DoctorScheduleModel>>
{
    public Task<Result<SchedulingPage<DoctorScheduleModel>>> Handle(
        ListDoctorSchedulesQuery query,
        CancellationToken cancellationToken)
    {
        Result page = SchedulingValidation.Page(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<SchedulingPage<DoctorScheduleModel>>(page.Error))
            : service.ListSchedulesAsync(
                query.DoctorId,
                query.From,
                query.To,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}

public sealed class ListDoctorExceptionsQueryHandler(ISchedulingQueryService service)
    : IQueryHandler<ListDoctorExceptionsQuery, SchedulingPage<DoctorExceptionModel>>
{
    public Task<Result<SchedulingPage<DoctorExceptionModel>>> Handle(
        ListDoctorExceptionsQuery query,
        CancellationToken cancellationToken)
    {
        Result page = SchedulingValidation.Page(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<SchedulingPage<DoctorExceptionModel>>(page.Error))
            : service.ListExceptionsAsync(
                query.DoctorId,
                query.From,
                query.To,
                query.IncludeCancelled,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}

public sealed class GetDepartmentAvailabilityQueryHandler(ISchedulingQueryService service)
    : IQueryHandler<GetDepartmentAvailabilityQuery, DepartmentAvailabilityModel>
{
    public Task<Result<DepartmentAvailabilityModel>> Handle(
        GetDepartmentAvailabilityQuery query,
        CancellationToken cancellationToken) =>
        service.GetDepartmentAvailabilityAsync(query.DepartmentId, query.At, cancellationToken);
}

public sealed class ListDepartmentClosuresQueryHandler(ISchedulingQueryService service)
    : IQueryHandler<ListDepartmentClosuresQuery, SchedulingPage<DepartmentClosureModel>>
{
    public Task<Result<SchedulingPage<DepartmentClosureModel>>> Handle(
        ListDepartmentClosuresQuery query,
        CancellationToken cancellationToken)
    {
        Result page = SchedulingValidation.Page(query.PageNumber, query.PageSize);
        return page.IsFailure
            ? Task.FromResult(Result.Failure<SchedulingPage<DepartmentClosureModel>>(page.Error))
            : service.ListClosuresAsync(
                query.DepartmentId,
                query.From,
                query.To,
                query.IncludeCancelled,
                query.PageNumber,
                query.PageSize,
                cancellationToken);
    }
}
