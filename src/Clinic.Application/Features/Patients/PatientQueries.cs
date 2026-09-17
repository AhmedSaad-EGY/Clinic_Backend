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

public sealed record GetPatientTimelineQuery(long PatientId, bool IncludeArchivedPatient,
    bool IncludeAdminOnlyNotes, PatientTimelineFilter Filter)
    : IQuery<PatientTimelinePage>;

public sealed class GetPatientTimelineQueryHandler
    : IQueryHandler<GetPatientTimelineQuery, PatientTimelinePage>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientTimelineQueryService _service;

    public GetPatientTimelineQueryHandler(ICurrentUser currentUser,
        IPatientTimelineQueryService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientTimelinePage>> Handle(GetPatientTimelineQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result validation = Validate(query.Filter);
        if (actor.IsFailure || validation.IsFailure)
        {
            return Task.FromResult(Result.Failure<PatientTimelinePage>(
                actor.IsFailure ? actor.Error : validation.Error));
        }

        return _service.GetAsync(query.PatientId, query.IncludeArchivedPatient,
            query.IncludeAdminOnlyNotes, query.Filter, cancellationToken);
    }

    private static Result Validate(PatientTimelineFilter filter)
    {
        Result page = PatientValidation.Page(filter.PageNumber, filter.PageSize);
        if (page.IsFailure)
        {
            return page;
        }

        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
        {
            return Result.Failure(PatientErrors.Validation(
                "تاريخ البداية يجب ألا يكون بعد تاريخ النهاية."));
        }

        if (filter.To == DateOnly.MaxValue || filter.RecordTypes.Any(type =>
            !Enum.IsDefined(type)))
        {
            return Result.Failure(PatientErrors.Validation(
                "مرشحات السجل الزمني غير صحيحة."));
        }

        return Result.Success();
    }
}

public sealed record GetPatientPaymentQuery(long PatientId, long PaymentId,
    bool IncludeArchivedPatient)
    : IQuery<PaymentModel>;

public sealed class GetPatientPaymentQueryHandler
    : IQueryHandler<GetPatientPaymentQuery, PaymentModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPaymentService _service;
    private readonly IPatientQueryService _patientService;

    public GetPatientPaymentQueryHandler(ICurrentUser currentUser, IPaymentService service,
        IPatientQueryService patientService)
    {
        _currentUser = currentUser;
        _service = service;
        _patientService = patientService;
    }

    public async Task<Result<PaymentModel>> Handle(GetPatientPaymentQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        if (actor.IsFailure)
        {
            return Result.Failure<PaymentModel>(actor.Error);
        }

        Result<PatientDetails> patient = await _patientService.GetPatientAsync(
            query.PatientId, query.IncludeArchivedPatient, cancellationToken);
        return patient.IsFailure
            ? Result.Failure<PaymentModel>(patient.Error)
            : await _service.GetForPatientAsync(query.PatientId, query.PaymentId,
                cancellationToken);
    }
}

public sealed record GetPatientRefundQuery(long PatientId, long RefundId,
    bool IncludeArchivedPatient)
    : IQuery<RefundModel>;

public sealed class GetPatientRefundQueryHandler
    : IQueryHandler<GetPatientRefundQuery, RefundModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IRefundService _service;
    private readonly IPatientQueryService _patientService;

    public GetPatientRefundQueryHandler(ICurrentUser currentUser, IRefundService service,
        IPatientQueryService patientService)
    {
        _currentUser = currentUser;
        _service = service;
        _patientService = patientService;
    }

    public async Task<Result<RefundModel>> Handle(GetPatientRefundQuery query,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        if (actor.IsFailure)
        {
            return Result.Failure<RefundModel>(actor.Error);
        }

        Result<PatientDetails> patient = await _patientService.GetPatientAsync(
            query.PatientId, query.IncludeArchivedPatient, cancellationToken);
        return patient.IsFailure
            ? Result.Failure<RefundModel>(patient.Error)
            : await _service.GetForPatientAsync(query.PatientId, query.RefundId,
                cancellationToken);
    }
}
