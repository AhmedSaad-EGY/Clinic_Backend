using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using Clinic.Domain.ClinicalRecords;

namespace Clinic.Application.Features.ClinicalRecords;

public sealed record GetPrescriptionQuery(long PrescriptionId,
    bool IncludeArchivedPatient) : IQuery<PrescriptionModel>;
public sealed record ListPatientPrescriptionsQuery(long PatientId,
    bool IncludeArchivedPatient, int PageNumber, int PageSize)
    : IQuery<ClinicalPage<PrescriptionSummary>>;
public sealed record ListPrescriptionRevisionsQuery(long PrescriptionId)
    : IQuery<IReadOnlyCollection<PrescriptionRevisionModel>>;
public sealed record GetPrescriptionRevisionQuery(long PrescriptionId,
    long RevisionId) : IQuery<PrescriptionRevisionModel>;
public sealed record GetFollowUpQuery(long FollowUpId,
    bool IncludeArchivedPatient) : IQuery<FollowUpModel>;
public sealed record SearchFollowUpsQuery(FollowUpSearch Search)
    : IQuery<ClinicalPage<FollowUpModel>>;

public sealed class GetPrescriptionQueryHandler(ICurrentUser currentUser,
    IClinicalRecordQueryService service)
    : IQueryHandler<GetPrescriptionQuery, PrescriptionModel>
{
    public Task<Result<PrescriptionModel>> Handle(GetPrescriptionQuery query,
        CancellationToken cancellationToken) => ClinicalQuerySupport.Auth(currentUser,
            () => service.GetAsync(query.PrescriptionId,
                query.IncludeArchivedPatient, cancellationToken));
}

public sealed class ListPatientPrescriptionsQueryHandler(ICurrentUser currentUser,
    IClinicalRecordQueryService service)
    : IQueryHandler<ListPatientPrescriptionsQuery, ClinicalPage<PrescriptionSummary>>
{
    public Task<Result<ClinicalPage<PrescriptionSummary>>> Handle(
        ListPatientPrescriptionsQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        Result page = ClinicalRecordValidation.Page(query.PageNumber, query.PageSize);
        return actor.IsFailure || page.IsFailure
            ? Task.FromResult(Result.Failure<ClinicalPage<PrescriptionSummary>>(
                actor.IsFailure ? actor.Error : page.Error))
            : service.ListPatientAsync(query.PatientId, query.IncludeArchivedPatient,
                query.PageNumber, query.PageSize, cancellationToken);
    }
}

public sealed class ListPrescriptionRevisionsQueryHandler(ICurrentUser currentUser,
    IClinicalRecordQueryService service)
    : IQueryHandler<ListPrescriptionRevisionsQuery,
        IReadOnlyCollection<PrescriptionRevisionModel>>
{
    public Task<Result<IReadOnlyCollection<PrescriptionRevisionModel>>> Handle(
        ListPrescriptionRevisionsQuery query, CancellationToken cancellationToken) =>
        ClinicalQuerySupport.Auth(currentUser,
            () => service.ListRevisionsAsync(query.PrescriptionId, cancellationToken));
}

public sealed class GetPrescriptionRevisionQueryHandler(ICurrentUser currentUser,
    IClinicalRecordQueryService service)
    : IQueryHandler<GetPrescriptionRevisionQuery, PrescriptionRevisionModel>
{
    public Task<Result<PrescriptionRevisionModel>> Handle(
        GetPrescriptionRevisionQuery query, CancellationToken cancellationToken) =>
        ClinicalQuerySupport.Auth(currentUser,
            () => service.GetRevisionAsync(query.PrescriptionId,
                query.RevisionId, cancellationToken));
}

public sealed class GetFollowUpQueryHandler(ICurrentUser currentUser,
    IClinicalRecordQueryService service)
    : IQueryHandler<GetFollowUpQuery, FollowUpModel>
{
    public Task<Result<FollowUpModel>> Handle(GetFollowUpQuery query,
        CancellationToken cancellationToken) => ClinicalQuerySupport.Auth(currentUser,
            () => service.GetFollowUpAsync(query.FollowUpId,
                query.IncludeArchivedPatient, cancellationToken));
}

public sealed class SearchFollowUpsQueryHandler(ICurrentUser currentUser,
    IClinicalRecordQueryService service)
    : IQueryHandler<SearchFollowUpsQuery, ClinicalPage<FollowUpModel>>
{
    public Task<Result<ClinicalPage<FollowUpModel>>> Handle(SearchFollowUpsQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        Result page = ClinicalRecordValidation.Page(query.Search.PageNumber,
            query.Search.PageSize);
        bool invalid = query.Search.PatientId <= 0 || query.Search.DepartmentId <= 0 ||
            query.Search.DoctorId <= 0 ||
            query.Search.Status.HasValue && !Enum.IsDefined(query.Search.Status.Value) ||
            query.Search.FromDate > query.Search.ToDate;
        if (actor.IsFailure || page.IsFailure || invalid)
            return Task.FromResult(Result.Failure<ClinicalPage<FollowUpModel>>(
                actor.IsFailure ? actor.Error : page.IsFailure ? page.Error
                    : ClinicalRecordErrors.Validation("فلاتر المتابعة غير صحيحة.")));
        return service.SearchFollowUpsAsync(query.Search, cancellationToken);
    }
}

file static class ClinicalQuerySupport
{
    public static Task<Result<T>> Auth<T>(ICurrentUser currentUser,
        Func<Task<Result<T>>> action)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        return actor.IsFailure ? Task.FromResult(Result.Failure<T>(actor.Error)) : action();
    }
}
