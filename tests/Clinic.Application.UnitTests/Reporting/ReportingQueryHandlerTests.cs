using Clinic.Application.Abstractions.Reporting;
using Clinic.Application.Common;
using Clinic.Application.Features.Reporting;

namespace Clinic.Application.UnitTests.Reporting;

public sealed class ReportingQueryHandlerTests
{
    [Fact]
    public async Task DashboardRejectsMaximumDate()
    {
        FakeReportingService service = new();
        GetAdminDashboardQueryHandler handler = new(service);

        Result<AdminDashboardSummaryModel> result = await handler.Handle(
            new GetAdminDashboardQuery(DateOnly.MaxValue), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task FinancialReportRejectsRangeLongerThan366Days()
    {
        FakeReportingService service = new();
        GetFinancialReportQueryHandler handler = new(service);

        Result<FinancialReportModel> result = await handler.Handle(
            new GetFinancialReportQuery(new FinancialReportFilter(
                new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2),
                null, null, null, null, null, null, null)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task OperationalReportRejectsInvertedAgeRange()
    {
        FakeReportingService service = new();
        GetOperationalReportQueryHandler handler = new(service);

        Result<OperationalReportModel> result = await handler.Handle(
            new GetOperationalReportQuery(new OperationalReportFilter(
                new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                null, null, null, null, null, 50, 20, null)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task ShiftReportRejectsInvalidPagination()
    {
        FakeReportingService service = new();
        GetShiftReportQueryHandler handler = new(service);

        Result<ShiftReportPage> result = await handler.Handle(
            new GetShiftReportQuery(new ShiftReportFilter(
                new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                null, null, 0, 20)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task AuditReportRejectsOversizedSearchText()
    {
        FakeReportingService service = new();
        GetAuditLogsQueryHandler handler = new(service);

        Result<AuditLogPage> result = await handler.Handle(
            new GetAuditLogsQuery(new AuditLogFilter(
                new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31),
                null, null, new string('x', 101), null, null, 1, 20)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.False(service.WasCalled);
    }

    private sealed class FakeReportingService : IReportingQueryService
    {
        public bool WasCalled { get; private set; }

        public Task<Result<AdminDashboardSummaryModel>> GetDashboardAsync(
            DateOnly? reportDate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<FinancialReportModel>> GetFinancialAsync(
            FinancialReportFilter filter, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new NotSupportedException();
        }

        public Task<Result<OperationalReportModel>> GetOperationalAsync(
            OperationalReportFilter filter, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new NotSupportedException();
        }

        public Task<Result<ComparisonReportModel>> GetComparisonAsync(
            ComparisonReportFilter filter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<ShiftReportPage>> GetShiftsAsync(ShiftReportFilter filter,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new NotSupportedException();
        }

        public Task<Result<AuditLogPage>> GetAuditLogsAsync(AuditLogFilter filter,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new NotSupportedException();
        }
    }
}
