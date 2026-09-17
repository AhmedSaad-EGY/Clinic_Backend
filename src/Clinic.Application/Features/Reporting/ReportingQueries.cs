namespace Clinic.Application.Features.Reporting;

public static class ReportingErrors
{
    public static ResultError Validation(string message) =>
        new("reporting.validation", message);

    public static ResultError ResultTooLarge => new("reporting.result_too_large",
        "نتيجة التقرير كبيرة جدًا. قلّل الفترة أو استخدم فلاتر إضافية.");
}

public sealed record GetAdminDashboardQuery(DateOnly? ReportDate)
    : IQuery<AdminDashboardSummaryModel>;
public sealed record GetFinancialReportQuery(FinancialReportFilter Filter)
    : IQuery<FinancialReportModel>;
public sealed record GetOperationalReportQuery(OperationalReportFilter Filter)
    : IQuery<OperationalReportModel>;
public sealed record GetComparisonReportQuery(ComparisonReportFilter Filter)
    : IQuery<ComparisonReportModel>;
public sealed record GetShiftReportQuery(ShiftReportFilter Filter)
    : IQuery<ShiftReportPage>;
public sealed record GetAuditLogsQuery(AuditLogFilter Filter)
    : IQuery<AuditLogPage>;

public sealed class GetAdminDashboardQueryHandler(IReportingQueryService service)
    : IQueryHandler<GetAdminDashboardQuery, AdminDashboardSummaryModel>
{
    public Task<Result<AdminDashboardSummaryModel>> Handle(
        GetAdminDashboardQuery query, CancellationToken cancellationToken) =>
        query.ReportDate == DateOnly.MaxValue
            ? Task.FromResult(Result.Failure<AdminDashboardSummaryModel>(
                ReportingErrors.Validation("تاريخ لوحة التحكم غير صحيح.")))
            : service.GetDashboardAsync(query.ReportDate, cancellationToken);
}

public sealed class GetFinancialReportQueryHandler(IReportingQueryService service)
    : IQueryHandler<GetFinancialReportQuery, FinancialReportModel>
{
    public Task<Result<FinancialReportModel>> Handle(GetFinancialReportQuery query,
        CancellationToken cancellationToken)
    {
        Result validation = ReportingValidation.Range(
            query.Filter.From, query.Filter.To, query.Filter.DepartmentId,
            query.Filter.ServiceId, query.Filter.DoctorId,
            query.Filter.SecretaryUserId, query.Filter.ShiftId,
            query.Filter.PaymentMethodId, query.Filter.PatientId);
        return validation.IsFailure
            ? Task.FromResult(Result.Failure<FinancialReportModel>(validation.Error))
            : service.GetFinancialAsync(query.Filter, cancellationToken);
    }
}

public sealed class GetOperationalReportQueryHandler(IReportingQueryService service)
    : IQueryHandler<GetOperationalReportQuery, OperationalReportModel>
{
    public Task<Result<OperationalReportModel>> Handle(GetOperationalReportQuery query,
        CancellationToken cancellationToken)
    {
        OperationalReportFilter filter = query.Filter;
        Result validation = ReportingValidation.Range(filter.From, filter.To,
            filter.DepartmentId, filter.ServiceId, filter.DoctorId);
        if (validation.IsFailure || filter.MinimumAge is < 0 or > 150 ||
            filter.MaximumAge is < 0 or > 150 ||
            filter.MinimumAge > filter.MaximumAge || filter.Area?.Length > 150 ||
            filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value) ||
            filter.Gender.HasValue && !Enum.IsDefined(filter.Gender.Value))
        {
            return Task.FromResult(Result.Failure<OperationalReportModel>(
                validation.IsFailure ? validation.Error : ReportingErrors.Validation(
                    "فلاتر التقرير التشغيلي غير صحيحة.")));
        }

        return service.GetOperationalAsync(filter, cancellationToken);
    }
}

public sealed class GetComparisonReportQueryHandler(IReportingQueryService service)
    : IQueryHandler<GetComparisonReportQuery, ComparisonReportModel>
{
    public Task<Result<ComparisonReportModel>> Handle(GetComparisonReportQuery query,
        CancellationToken cancellationToken)
    {
        Result validation = ReportingValidation.Range(query.Filter.From,
            query.Filter.To, query.Filter.DepartmentId, query.Filter.DoctorId);
        return validation.IsFailure || !Enum.IsDefined(query.Filter.GroupBy)
            ? Task.FromResult(Result.Failure<ComparisonReportModel>(
                validation.IsFailure ? validation.Error : ReportingErrors.Validation(
                    "نوع المقارنة غير صحيح.")))
            : service.GetComparisonAsync(query.Filter, cancellationToken);
    }
}

public sealed class GetShiftReportQueryHandler(IReportingQueryService service)
    : IQueryHandler<GetShiftReportQuery, ShiftReportPage>
{
    public Task<Result<ShiftReportPage>> Handle(GetShiftReportQuery query,
        CancellationToken cancellationToken)
    {
        ShiftReportFilter filter = query.Filter;
        Result validation = ReportingValidation.Range(filter.From, filter.To,
            filter.SecretaryUserId);
        return validation.IsFailure || filter.Status.HasValue &&
            !Enum.IsDefined(filter.Status.Value) ||
            !ReportingValidation.ValidPage(filter.PageNumber, filter.PageSize)
            ? Task.FromResult(Result.Failure<ShiftReportPage>(validation.IsFailure
                ? validation.Error : ReportingErrors.Validation(
                    "فلاتر تقرير الشيفتات غير صحيحة.")))
            : service.GetShiftsAsync(filter, cancellationToken);
    }
}

public sealed class GetAuditLogsQueryHandler(IReportingQueryService service)
    : IQueryHandler<GetAuditLogsQuery, AuditLogPage>
{
    public Task<Result<AuditLogPage>> Handle(GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        AuditLogFilter filter = query.Filter;
        Result validation = ReportingValidation.Range(filter.From, filter.To,
            filter.ActorUserId);
        bool invalidText = filter.Action?.Length > 100 ||
            filter.EntityType?.Length > 100 || filter.EntityId?.Length > 100;
        return validation.IsFailure || invalidText || filter.ActorType.HasValue &&
            !Enum.IsDefined(filter.ActorType.Value) ||
            !ReportingValidation.ValidPage(filter.PageNumber, filter.PageSize)
            ? Task.FromResult(Result.Failure<AuditLogPage>(validation.IsFailure
                ? validation.Error : ReportingErrors.Validation(
                    "فلاتر سجل العمليات غير صحيحة.")))
            : service.GetAuditLogsAsync(filter, cancellationToken);
    }
}

internal static class ReportingValidation
{
    public static Result Range(DateOnly from, DateOnly to, params long?[] ids)
    {
        if (from == default || to == default || to == DateOnly.MaxValue || from > to ||
            to.DayNumber - from.DayNumber > 365 || ids.Any(id => id is <= 0))
        {
            return Result.Failure(ReportingErrors.Validation(
                "الفترة يجب أن تكون صحيحة ولا تتجاوز 366 يومًا، والمعرفات موجبة."));
        }

        return Result.Success();
    }

    public static bool ValidPage(int pageNumber, int pageSize) =>
        pageNumber >= 1 && pageSize is >= 1 and <= 100;
}
