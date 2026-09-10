using Clinic.Api.Contracts.Reporting;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Reporting;
using Clinic.Application.Features.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin")]
public sealed class AdminReportsController : ControllerBase
{
    [HttpGet("dashboard/summary")]
    public async Task<ActionResult<AdminDashboardSummaryModel>> Dashboard(
        [FromQuery] DateOnly? reportDate, GetAdminDashboardQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetAdminDashboardQuery(reportDate),
                cancellationToken));

    [HttpGet("reports/financial")]
    public async Task<ActionResult<FinancialReportModel>> Financial(
        [FromQuery] FinancialReportRequest request,
        GetFinancialReportQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetFinancialReportQuery(new FinancialReportFilter(
                request.From, request.To, request.DepartmentId, request.ServiceId,
                request.DoctorId, request.SecretaryUserId, request.ShiftId,
                request.PaymentMethodId, request.PatientId)), cancellationToken));

    [HttpGet("reports/operations")]
    public async Task<ActionResult<OperationalReportModel>> Operations(
        [FromQuery] OperationalReportRequest request,
        GetOperationalReportQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetOperationalReportQuery(new OperationalReportFilter(
                request.From, request.To, request.DepartmentId, request.ServiceId,
                request.DoctorId, request.Status, request.Gender, request.MinimumAge,
                request.MaximumAge, request.Area)), cancellationToken));

    [HttpGet("reports/comparison")]
    public async Task<ActionResult<ComparisonReportModel>> Comparison(
        [FromQuery] ComparisonReportRequest request,
        GetComparisonReportQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetComparisonReportQuery(new ComparisonReportFilter(
                request.From, request.To, request.GroupBy, request.DepartmentId,
                request.DoctorId)), cancellationToken));

    [HttpGet("reports/shifts")]
    public async Task<ActionResult<ShiftReportPage>> Shifts(
        [FromQuery] ShiftReportRequest request, GetShiftReportQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetShiftReportQuery(new ShiftReportFilter(
                request.From, request.To, request.SecretaryUserId, request.Status,
                request.PageNumber, request.PageSize)), cancellationToken));

    [HttpGet("audit-logs")]
    public async Task<ActionResult<AuditLogPage>> AuditLogs(
        [FromQuery] AuditLogRequest request, GetAuditLogsQueryHandler handler,
        CancellationToken cancellationToken) => this.ToActionResult(
            await handler.Handle(new GetAuditLogsQuery(new AuditLogFilter(
                request.From, request.To, request.ActorUserId, request.ActorType,
                request.Action, request.EntityType, request.EntityId,
                request.PageNumber, request.PageSize)), cancellationToken));
}
