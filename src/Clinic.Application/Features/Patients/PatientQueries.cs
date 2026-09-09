using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using Clinic.Domain.Patients;

namespace Clinic.Application.Features.Patients;

public sealed record ListPatientsQuery(string? Search, PatientGender? Gender, string? Area,
    bool IncludeArchived, int PageNumber, int PageSize)
    : IQuery<PatientPage<PatientSummary>>;

public sealed class ListPatientsQueryHandler
    : IQueryHandler<ListPatientsQuery, PatientPage<PatientSummary>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientQueryService _service;

    public ListPatientsQueryHandler(ICurrentUser currentUser, IPatientQueryService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientPage<PatientSummary>>> Handle(ListPatientsQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result page = PatientValidation.Page(query.PageNumber, query.PageSize);
        if (actor.IsFailure || page.IsFailure)
        {
            return Task.FromResult(Result.Failure<PatientPage<PatientSummary>>(
                actor.IsFailure ? actor.Error : page.Error));
        }

        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        EgyptianMobileNumber.TryNormalize(search, out string? phone);
        return _service.ListPatientsAsync(search, phone,
            PatientValidation.ParseFileNumber(search), query.Gender,
            string.IsNullOrWhiteSpace(query.Area) ? null : query.Area.Trim(),
            query.IncludeArchived, query.PageNumber, query.PageSize, cancellationToken);
    }
}

public sealed record GetPatientQuery(long PatientId, bool IncludeArchived)
    : IQuery<PatientDetails>;

public sealed class GetPatientQueryHandler : IQueryHandler<GetPatientQuery, PatientDetails>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientQueryService _service;

    public GetPatientQueryHandler(ICurrentUser currentUser, IPatientQueryService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientDetails>> Handle(GetPatientQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        return actor.IsFailure
            ? Task.FromResult(Result.Failure<PatientDetails>(actor.Error))
            : _service.GetPatientAsync(query.PatientId, query.IncludeArchived, cancellationToken);
    }
}

public sealed record ListPatientNotesQuery(long PatientId, bool IncludeAdminOnly,
    bool IncludeArchived, int PageNumber, int PageSize)
    : IQuery<PatientPage<PatientNoteModel>>;

public sealed class ListPatientNotesQueryHandler
    : IQueryHandler<ListPatientNotesQuery, PatientPage<PatientNoteModel>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientQueryService _service;

    public ListPatientNotesQueryHandler(ICurrentUser currentUser, IPatientQueryService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientPage<PatientNoteModel>>> Handle(ListPatientNotesQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result page = PatientValidation.Page(query.PageNumber, query.PageSize);
        if (actor.IsFailure || page.IsFailure)
        {
            return Task.FromResult(Result.Failure<PatientPage<PatientNoteModel>>(
                actor.IsFailure ? actor.Error : page.Error));
        }

        return query.IncludeAdminOnly
            ? _service.ListAllNotesAsync(query.PatientId, query.IncludeArchived,
                query.PageNumber, query.PageSize, cancellationToken)
            : _service.ListStaffNotesAsync(query.PatientId, includeArchived: false,
                query.PageNumber, query.PageSize, cancellationToken);
    }
}

public sealed record ListTreatmentHistoryQuery(long PatientId, bool IncludeArchived,
    int PageNumber, int PageSize) : IQuery<PatientPage<TreatmentHistoryModel>>;

public sealed class ListTreatmentHistoryQueryHandler
    : IQueryHandler<ListTreatmentHistoryQuery, PatientPage<TreatmentHistoryModel>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientQueryService _service;

    public ListTreatmentHistoryQueryHandler(ICurrentUser currentUser,
        IPatientQueryService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientPage<TreatmentHistoryModel>>> Handle(
        ListTreatmentHistoryQuery query, CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result page = PatientValidation.Page(query.PageNumber, query.PageSize);
        if (actor.IsFailure || page.IsFailure)
        {
            return Task.FromResult(Result.Failure<PatientPage<TreatmentHistoryModel>>(
                actor.IsFailure ? actor.Error : page.Error));
        }

        return _service.ListTreatmentHistoryAsync(query.PatientId, query.IncludeArchived,
            query.PageNumber, query.PageSize, cancellationToken);
    }
}
