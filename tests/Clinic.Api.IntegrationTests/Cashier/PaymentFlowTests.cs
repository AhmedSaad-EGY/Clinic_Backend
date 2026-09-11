using System.Net;
using System.Net.Http.Json;
using Clinic.Api.IntegrationTests.Identity;
using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Abstractions.Cashier;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Domain.Appointments;
using Clinic.Domain.Cashier;
using Clinic.Domain.Catalog;
using Clinic.Domain.Patients;
using Clinic.Domain.Packages;
using Clinic.Domain.Scheduling;
using Clinic.Infrastructure.Identity;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Clinic.Api.IntegrationTests.Cashier;

public sealed class PaymentFlowTests : IClassFixture<IdentitySqlServerFixture>
{
    private readonly IdentitySqlServerFixture _fixture;

    public PaymentFlowTests(IdentitySqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PaymentFlowIsAtomicIdempotentAndUpdatesShiftSummary()
    {
        DateOnly clinicDate = new(2026, 9, 27);
        DateTimeOffset start = ClinicInstant(clinicDate, new TimeOnly(10, 0));
        _fixture.Clock.SetUtcNow(start.AddHours(-1));

        using HttpClient admin = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(admin, "integration.admin",
            IdentitySqlServerFixture.InitialAdminPassword,
            IdentitySqlServerFixture.UpdatedAdminPassword);
        using (HttpResponseMessage openApi = await admin.GetAsync(
            "/swagger/v1/swagger.json"))
        {
            string document = await openApi.Content.ReadAsStringAsync();
            Assert.True(openApi.IsSuccessStatusCode,
                $"{openApi.StatusCode}: {document}");
            Assert.Contains("/api/cashier/payments", document);
            Assert.Contains("/api/admin/cashier/payments", document);
            Assert.Contains("/collection-summary", document);
            Assert.Contains(
                "/api/cashier/approval-requests/appointment-cancellations",
                document);
            Assert.Contains(
                "/api/admin/cashier/approval-requests/{requestId}/approve",
                document);
            Assert.Contains("/api/cashier/approval-requests/{requestId}/refunds",
                document);
            Assert.Contains("/api/admin/cashier/refunds/{refundId}", document);
            Assert.Contains("patientPackageIds", document);
            Assert.Contains("/api/patient-packages/{patientPackageId}/booking-options",
                document);
            Assert.Contains("packageCoveredAmount", document);
        }

        SecretaryResponse secretary = await CreateSecretaryAsync(admin);
        ShiftResponse shift = Assert.Single((await PostAndReadAsync<GenerateShiftsRequest,
            GeneratedShiftsResponse>(admin, "/api/admin/cashier/shifts/generate", new(
                secretary.Id, clinicDate, clinicDate, [ClinicWeekday(clinicDate)],
                new TimeOnly(10, 0), new TimeOnly(14, 0)))).Items);
        SeededPaymentTargets seeded = await SeedPaymentTargetsAsync(clinicDate, start);
        long[] appointmentIds = seeded.AppointmentIds;
        ReportingComparisonResponse unpaidComparison = await GetAsync<
            ReportingComparisonResponse>(admin,
            "/api/admin/reports/comparison?from=2026-09-27&to=2026-09-27" +
            "&groupBy=3");
        ReportingComparisonPoint unpaidDepartment = Assert.Single(
            unpaidComparison.Points, item => item.Key == seeded.DepartmentId.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2, unpaidDepartment.AppointmentCount);
        Assert.Equal(0, unpaidDepartment.Net);

        using HttpClient cashier = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(cashier, secretary.UserName,
            "SecretaryPass1", "SecretaryPass2");
        _fixture.Clock.SetUtcNow(start);
        PostPaymentRequest request = new(appointmentIds,
            [new(1, 200m, null), new(2, 300m, "VISA-123\nDETAIL")], "NOTE");
        using (HttpResponseMessage beforeOpening = await PostPaymentAsync(cashier,
            request, Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.Conflict, beforeOpening.StatusCode);
        }

        ShiftResponse current = await GetAsync<ShiftResponse>(cashier,
            "/api/cashier/shifts/current");
        ShiftResponse opened = await PostAndReadAsync<OpeningBalanceRequest,
            ShiftResponse>(cashier,
            $"/api/cashier/shifts/{shift.Id}/opening-balance",
            new(100m, current.RowVersion));

        PaymentMethodResponse[] methods = await GetAsync<PaymentMethodResponse[]>(cashier,
            "/api/cashier/payment-methods");
        Assert.Equal(["CASH", "VISA", "INSTAPAY", "WALLET"],
            methods.Select(item => item.Code));

        await using (AsyncServiceScope paymentScope =
            _fixture.Services.CreateAsyncScope())
        {
            IPaymentService paymentService = paymentScope.ServiceProvider
                .GetRequiredService<IPaymentService>();
            _fixture.Clock.SetUtcNowSequence(start, opened.GraceEndsAt);
            Result<PostedPaymentModel> crossedGrace = await paymentService.PostAsync(
                secretary.Id, Guid.NewGuid(), new PostPaymentInput(null, appointmentIds,
                    [new PaymentMethodInput(1, 200m, null),
                        new PaymentMethodInput(2, 300m, "VISA-123")],
                    "تحصيل اختبار", null), false, CancellationToken.None);
            Assert.True(crossedGrace.IsFailure);
            Assert.Equal("cashier.conflict", crossedGrace.Error.Code);
        }

        _fixture.Clock.SetUtcNow(start);
        Guid key = Guid.NewGuid();

        HttpResponseMessage[] concurrent = await Task.WhenAll(
            PostPaymentAsync(cashier, request, key),
            PostPaymentAsync(cashier, request, key));
        Assert.Single(concurrent, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrent, item => item.StatusCode == HttpStatusCode.OK);
        HttpResponseMessage replay = concurrent.Single(item =>
            item.StatusCode == HttpStatusCode.OK);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        PaymentResponse payment = await concurrent.Single(item =>
            item.StatusCode == HttpStatusCode.Created).Content
            .ReadFromJsonAsync<PaymentResponse>() ?? throw new InvalidOperationException();
        foreach (HttpResponseMessage response in concurrent) response.Dispose();

        Assert.StartsWith("PAY-20260927-", payment.TransactionNumber,
            StringComparison.Ordinal);
        Assert.Equal(500m, payment.TotalAmount);
        Assert.Equal(2, payment.AppointmentAllocations.Count);
        ReportingFinancialResponse financialReport = await GetAsync<
            ReportingFinancialResponse>(admin,
            "/api/admin/reports/financial?from=2026-09-27&to=2026-09-27" +
            $"&departmentId={seeded.DepartmentId}");
        Assert.Equal(financialReport.Collected,
            financialReport.PaymentMethods.Sum(item => item.Collected));
        Assert.Equal(financialReport.Collected,
            financialReport.Departments.Sum(item => item.Collected));

        PostPaymentRequest packageRequest = new([], [new(1, 400m, null)],
            "تحصيل باقة", [seeded.PatientPackageId]);
        using HttpResponseMessage packagePaymentResponse = await PostPaymentAsync(cashier,
            packageRequest, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, packagePaymentResponse.StatusCode);
        PaymentResponse packagePayment = await packagePaymentResponse.Content
            .ReadFromJsonAsync<PaymentResponse>() ?? throw new InvalidOperationException();
        PackagePaymentAllocationResponse packageAllocation =
            Assert.Single(packagePayment.PackageAllocations);
        Assert.Equal(seeded.PatientPackageId, packageAllocation.PatientPackageId);
        Assert.Equal(400m, packageAllocation.Amount);
        PatientPackagePaymentResponse paidPackage =
            await GetAsync<PatientPackagePaymentResponse>(cashier,
                $"/api/patient-packages/{seeded.PatientPackageId}");
        Assert.Equal(PatientPackagePaymentStatus.Paid, paidPackage.PaymentStatus);
        Assert.Equal(packagePayment.Id, paidPackage.Payment?.PaymentId);
        Assert.Equal(packagePayment.TransactionNumber,
            paidPackage.Payment?.TransactionNumber);
        PatientTimelineResponse financialTimeline = await GetAsync<PatientTimelineResponse>(
            cashier, $"/api/patients/{seeded.PatientId}/timeline" +
                "?recordTypes=2&recordTypes=4");
        Assert.Contains(financialTimeline.Items,
            item => item.RecordType == PatientTimelineRecordType.Payment);
        Assert.Contains(financialTimeline.Items,
            item => item.RecordType == PatientTimelineRecordType.PatientPackage);
        PackageBookingOptionsResponse bookingOptions = await GetAsync<
            PackageBookingOptionsResponse>(cashier,
            $"/api/patient-packages/{seeded.PatientPackageId}/booking-options" +
            $"?startAt={Uri.EscapeDataString(start.AddHours(3).ToString("O"))}");
        Assert.True(bookingOptions.CanBook);
        PackageBookingOptionServiceResponse firstOption = Assert.Single(
            bookingOptions.Services, item => item.ServiceId == seeded.ServiceId);
        Assert.Equal(2, firstOption.AvailableSessions);
        Assert.True(firstOption.CanBook);

        await SetServiceActiveAsync(seeded.SecondServiceId, false);
        PackageBookingOptionsResponse partiallyAvailable = await GetAsync<
            PackageBookingOptionsResponse>(cashier,
            $"/api/patient-packages/{seeded.PatientPackageId}/booking-options" +
            $"?startAt={Uri.EscapeDataString(start.AddHours(3).ToString("O"))}");
        Assert.True(partiallyAvailable.CanBook);
        Assert.True(Assert.Single(partiallyAvailable.Services,
            item => item.ServiceId == seeded.ServiceId).CanBook);
        Assert.False(Assert.Single(partiallyAvailable.Services,
            item => item.ServiceId == seeded.SecondServiceId).CanBook);
        await SetServiceActiveAsync(seeded.SecondServiceId, true);

        Guid appointmentKey = Guid.NewGuid();
        CreatePackageAppointmentRequest packageAppointmentRequest = new(seeded.PatientId,
            seeded.DepartmentId, start.AddHours(3),
            [new(seeded.ServiceId, seeded.DoctorId, 1, [])],
            seeded.PatientPackageId);
        using HttpResponseMessage packageAppointmentResponse = await CreatePostMessageAsync(
            cashier, "/api/appointments", packageAppointmentRequest, appointmentKey);
        string packageAppointmentBody = await packageAppointmentResponse.Content
            .ReadAsStringAsync();
        Assert.True(packageAppointmentResponse.StatusCode == HttpStatusCode.Created,
            $"{packageAppointmentResponse.StatusCode}: {packageAppointmentBody}");
        PackageAppointmentResponse packageAppointment = await packageAppointmentResponse.Content
            .ReadFromJsonAsync<PackageAppointmentResponse>() ??
            throw new InvalidOperationException();
        Assert.Equal(PaymentStatus.CoveredByPackage, packageAppointment.PaymentStatus);
        Assert.Equal(0m, packageAppointment.NetAmount);
        Assert.Equal(200m, packageAppointment.PackageCoveredAmount);
        Assert.Equal(PackageSessionBookingStatus.Reserved,
            Assert.Single(packageAppointment.Services).PackageSessionBooking?.Status);
        using (HttpResponseMessage replayedAppointment = await CreatePostMessageAsync(cashier,
            "/api/appointments", packageAppointmentRequest, appointmentKey))
        {
            Assert.Equal(HttpStatusCode.OK, replayedAppointment.StatusCode);
            Assert.Equal("true", replayedAppointment.Headers
                .GetValues("Idempotency-Replayed").Single());
        }
        using (HttpResponseMessage changedAppointment = await CreatePostMessageAsync(cashier,
            "/api/appointments", packageAppointmentRequest with
            { StartAt = start.AddHours(3).AddMinutes(15) }, appointmentKey))
        {
            Assert.Equal(HttpStatusCode.Conflict, changedAppointment.StatusCode);
        }

        _fixture.Clock.SetUtcNow(start.AddHours(3).AddMinutes(31));
        using (HttpResponseMessage completedPackageAppointment = await PostAsync(cashier,
            $"/api/appointments/{packageAppointment.Id}/complete",
            new AppointmentStateRequest(packageAppointment.RowVersion)))
        {
            Assert.Equal(HttpStatusCode.NoContent, completedPackageAppointment.StatusCode);
        }
        PackageAppointmentResponse completedAppointment = await GetAsync<
            PackageAppointmentResponse>(cashier,
            $"/api/appointments/{packageAppointment.Id}");
        Assert.Equal(AppointmentStatus.Completed, completedAppointment.Status);
        Assert.Equal(PackageSessionBookingStatus.Consumed,
            Assert.Single(completedAppointment.Services).PackageSessionBooking?.Status);
        PatientTimelineResponse packageTimeline = await GetAsync<PatientTimelineResponse>(
            cashier, $"/api/patients/{seeded.PatientId}/timeline" +
                "?recordTypes=1&recordTypes=5");
        Assert.Contains(packageTimeline.Items,
            item => item.RecordType == PatientTimelineRecordType.Appointment);
        Assert.Contains(packageTimeline.Items,
            item => item.RecordType == PatientTimelineRecordType.PackageSession);

        await AddDepartmentClosureAsync(seeded.DepartmentId, seeded.AdminUserId,
            start.AddHours(4), start.AddHours(4).AddMinutes(30));
        CreatePackageAppointmentRequest noShowRequest = packageAppointmentRequest with
        { StartAt = start.AddHours(4) };
        using HttpResponseMessage noShowCreateResponse = await CreatePostMessageAsync(cashier,
            "/api/appointments", noShowRequest, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, noShowCreateResponse.StatusCode);
        PackageAppointmentResponse noShowAppointment = await noShowCreateResponse.Content
            .ReadFromJsonAsync<PackageAppointmentResponse>() ??
            throw new InvalidOperationException();
        Assert.Equal(AppointmentStatus.Suspended, noShowAppointment.Status);
        PackageAppointmentResponse rescheduled = await PutAndReadAsync<
            UpdatePackageAppointmentRequest, PackageAppointmentResponse>(cashier,
            $"/api/appointments/{noShowAppointment.Id}", new(seeded.PatientId,
                seeded.DepartmentId, start.AddHours(4).AddMinutes(30),
                noShowRequest.Services, noShowAppointment.RowVersion,
                seeded.PatientPackageId));
        Assert.Equal(AppointmentStatus.Confirmed, rescheduled.Status);
        Assert.Equal(PackageSessionBookingStatus.Reserved,
            Assert.Single(rescheduled.Services).PackageSessionBooking?.Status);

        using (HttpResponseMessage exhaustedAvailability = await PostAsync(cashier,
            "/api/appointments/availability", packageAppointmentRequest with
            { StartAt = start.AddHours(5) }))
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                exhaustedAvailability.StatusCode);
        }
        using (HttpResponseMessage noShowResponse = await PostAsync(cashier,
            $"/api/appointments/{noShowAppointment.Id}/no-show",
            new AppointmentStateRequest(rescheduled.RowVersion)))
        {
            Assert.Equal(HttpStatusCode.NoContent, noShowResponse.StatusCode);
        }

        _fixture.Clock.SetUtcNow(start.AddMinutes(-1));
        CreatePackageAppointmentRequest cancellationRequest = packageAppointmentRequest with
        { StartAt = start.AddHours(5) };
        using HttpResponseMessage cancellationCreateResponse = await CreatePostMessageAsync(
            cashier, "/api/appointments", cancellationRequest, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, cancellationCreateResponse.StatusCode);
        PackageAppointmentResponse cancellationAppointment =
            await cancellationCreateResponse.Content
                .ReadFromJsonAsync<PackageAppointmentResponse>() ??
            throw new InvalidOperationException();
        ApprovalRequestResponse packageCancellation = await PostAndReadAsync<
            CreateCancellationApprovalRequest, ApprovalRequestResponse>(cashier,
            "/api/cashier/approval-requests/appointment-cancellations",
            new(cancellationAppointment.Id, cancellationAppointment.RowVersion,
                "إلغاء جلسة باقة"));
        Assert.Null(packageCancellation.RequestedAmount);
        Assert.Empty(packageCancellation.RefundableMethods);
        _ = await PostAndReadAsync<ReviewApprovalRequest, ApprovalRequestResponse>(admin,
            $"/api/admin/cashier/approval-requests/{packageCancellation.Id}/approve",
            new(packageCancellation.RowVersion, "اعتماد إلغاء جلسة الباقة"));
        PackageAppointmentResponse cancelledPackageAppointment = await GetAsync<
            PackageAppointmentResponse>(cashier,
            $"/api/appointments/{cancellationAppointment.Id}");
        Assert.Equal(AppointmentStatus.Cancelled, cancelledPackageAppointment.Status);
        await VerifyPackageAppointmentRetryAsync(seeded, start, secretary.Id);

        _fixture.Clock.SetUtcNow(start);
        using (HttpResponseMessage repeatedPackage = await PostPaymentAsync(cashier,
            packageRequest, Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.Conflict, repeatedPackage.StatusCode);
        }

        using HttpResponseMessage changedReplay = await PostPaymentAsync(cashier,
            request with
            {
                MethodAllocations =
                    [new(1, 200m, null), new(2, 300m, "VISA-123")],
                Note = "DETAIL\nNOTE"
            }, key);
        Assert.Equal(HttpStatusCode.Conflict, changedReplay.StatusCode);
        using HttpResponseMessage secondPayment = await PostPaymentAsync(cashier,
            request, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, secondPayment.StatusCode);

        ShiftSummaryResponse summary = await GetAsync<ShiftSummaryResponse>(cashier,
            $"/api/cashier/shifts/{shift.Id}/collection-summary");
        Assert.Equal(600m, summary.CashCollected);
        Assert.Equal(300m, summary.ElectronicCollected);
        Assert.Equal(900m, summary.TotalCollected);
        Assert.Equal(700m, summary.CurrentExpectedCash);
        Assert.Equal(2, summary.PaymentCount);

        await using (AsyncServiceScope scope = _fixture.Services.CreateAsyncScope())
        {
            ClinicDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<ClinicDbContext>();
            Assert.Equal(2, await dbContext.Payments.CountAsync());
            Assert.All(await dbContext.Appointments.Where(item =>
                appointmentIds.Contains(item.Id)).ToArrayAsync(), item =>
            {
                Assert.Equal(PaymentStatus.Paid, item.PaymentStatus);
                Assert.Equal(AppointmentStatus.Confirmed, item.Status);
            });
            Assert.True(await dbContext.AuditLogs.AnyAsync(item =>
                item.Action == "cashier.payment_posted"));
            PatientPackage patientPackage = await dbContext.PatientPackages.SingleAsync(
                item => item.Id == seeded.PatientPackageId);
            Assert.Equal(PatientPackagePaymentStatus.Paid, patientPackage.PaymentStatus);
            Assert.Equal(start, patientPackage.ActivationWindowStartedAt);
            Assert.Equal(start.AddDays(14), patientPackage.ActivationDeadlineAt);
            Assert.True(await dbContext.AuditLogs.AnyAsync(item =>
                item.Action == "packages.patient_package.paid"));
            AppointmentPaymentAllocationResponse allocation =
                payment.AppointmentAllocations.First();
            long paymentPatientId = (await dbContext.Payments.SingleAsync(item =>
                item.Id == payment.Id)).PatientId;
            await Assert.ThrowsAsync<SqlException>(() =>
                dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [cashier].[AppointmentPaymentAllocations]
                        ([PaymentId], [AppointmentId], [PatientId], [Amount])
                    VALUES ({payment.Id}, {allocation.AppointmentId},
                        {paymentPatientId}, {allocation.Amount});
                    """));

            PackageSessionBooking consumedBooking = await dbContext
                .PackageSessionBookings.AsNoTracking().SingleAsync(item =>
                    item.AppointmentId == packageAppointment.Id);
            PackageSessionBooking releasedBooking = await dbContext
                .PackageSessionBookings.AsNoTracking().Where(item =>
                    item.AppointmentId == noShowAppointment.Id &&
                    item.Status == PackageSessionBookingStatus.Released)
                .OrderByDescending(item => item.Id).FirstAsync();
            await Assert.ThrowsAsync<SqlException>(() => dbContext.Database
                .ExecuteSqlInterpolatedAsync($"""
                    UPDATE [packages].[PackageSessionBookings]
                    SET [AppointmentId] = {cancellationAppointment.Id}
                    WHERE [Id] = {releasedBooking.Id};
                    """));
            long otherPackageSessionId = await dbContext.PackageSessions.AsNoTracking()
                .Where(item => item.PatientPackageId == seeded.ConcurrentPatientPackageId &&
                    item.ServiceId == seeded.ServiceId)
                .Select(item => item.Id).FirstAsync();
            await Assert.ThrowsAsync<SqlException>(() => dbContext.Database
                .ExecuteSqlInterpolatedAsync($"""
                    UPDATE [packages].[PackageSessionBookings]
                    SET [PackageSessionId] = {otherPackageSessionId},
                        [PatientPackageId] = {seeded.ConcurrentPatientPackageId}
                    WHERE [Id] = {releasedBooking.Id};
                    """));
            await Assert.ThrowsAsync<SqlException>(() => dbContext.Database
                .ExecuteSqlInterpolatedAsync($"""
                    UPDATE [packages].[PackageSessionBookings]
                    SET [PackageSessionId] = {consumedBooking.PackageSessionId},
                        [Status] = {(int)PackageSessionBookingStatus.Reserved},
                        [ReleasedAt] = NULL,
                        [ConsumedAt] = NULL
                    WHERE [Id] = {releasedBooking.Id};
                    """));
        }

        PatientPackagePaymentResponse packageBeforeExtension = await GetAsync<
            PatientPackagePaymentResponse>(cashier,
            $"/api/patient-packages/{seeded.PatientPackageId}");
        PatientPackagePaymentResponse extendedPackage = await PostAndReadAsync<
            ExtendPatientPackageRequest, PatientPackagePaymentResponse>(admin,
            $"/api/admin/patient-packages/{seeded.PatientPackageId}/extend",
            new(PatientPackageExtensionType.UsageExpiry, start.AddDays(120),
                "تمديد بعد التحصيل", packageBeforeExtension.RowVersion));
        Assert.Equal(packagePayment.Id, extendedPackage.Payment?.PaymentId);
        Assert.Equal(packagePayment.TransactionNumber,
            extendedPackage.Payment?.TransactionNumber);

        PostPaymentRequest concurrentPackageRequest = new([], [new(1, 400m, null)],
            "تحصيل متزامن", [seeded.ConcurrentPatientPackageId]);
        HttpResponseMessage[] packageConcurrency = await Task.WhenAll(
            PostPaymentAsync(cashier, concurrentPackageRequest, Guid.NewGuid()),
            PostPaymentAsync(cashier, concurrentPackageRequest, Guid.NewGuid()));
        Assert.Single(packageConcurrency,
            item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(packageConcurrency,
            item => item.StatusCode == HttpStatusCode.Conflict);
        foreach (HttpResponseMessage response in packageConcurrency) response.Dispose();

        AppointmentVersionResponse firstAppointment =
            await GetAsync<AppointmentVersionResponse>(cashier,
                $"/api/appointments/{appointmentIds[0]}");
        ApprovalRequestResponse firstRequest = await PostAndReadAsync<
            CreateCancellationApprovalRequest, ApprovalRequestResponse>(cashier,
            "/api/cashier/approval-requests/appointment-cancellations",
            new(appointmentIds[0], firstAppointment.RowVersion, "طلب المريض"));
        Assert.Equal(300m, firstRequest.RequestedAmount);
        using (HttpResponseMessage forbiddenApproval = await PostAsync(cashier,
            $"/api/admin/cashier/approval-requests/{firstRequest.Id}/approve",
            new ReviewApprovalRequest(firstRequest.RowVersion, "غير مسموح")))
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                forbiddenApproval.StatusCode);
        }

        ApprovalRequestResponse firstApproved = await PostAndReadAsync<
            ReviewApprovalRequest, ApprovalRequestResponse>(admin,
            $"/api/admin/cashier/approval-requests/{firstRequest.Id}/approve",
            new(firstRequest.RowVersion, "تمت مراجعة الطلب"));
        RefundableMethodResponse visa = Assert.Single(firstApproved.RefundableMethods,
            item => item.Code == "VISA");

        ExecuteRefundRequest firstRefundRequest = new(
            [new(visa.OriginalAllocationId, 300m, "VISA-REF\nDETAIL")],
            "NOTE");
        using (HttpResponseMessage forbiddenRefund = await CreatePostMessageAsync(
            admin, $"/api/cashier/approval-requests/{firstApproved.Id}/refunds",
            firstRefundRequest, Guid.NewGuid()))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenRefund.StatusCode);
        }
        Guid firstRefundKey = Guid.NewGuid();
        PostedRefundResponse firstRefund = await PostRefundAsync(cashier,
            firstApproved.Id, firstRefundRequest, firstRefundKey,
            HttpStatusCode.Created);
        Assert.False(firstRefund.WasReplayed);
        Assert.Equal(300m, firstRefund.Refund.Amount);
        Assert.False(firstRefund.IsExpectedCashNegative);
        PostedRefundResponse firstRefundReplay = await PostRefundAsync(cashier,
            firstApproved.Id, firstRefundRequest, firstRefundKey, HttpStatusCode.OK);
        Assert.True(firstRefundReplay.WasReplayed);
        Assert.Equal(firstRefund.Refund.Id, firstRefundReplay.Refund.Id);
        using (HttpResponseMessage ambiguousReplay = await CreatePostMessageAsync(
            cashier, $"/api/cashier/approval-requests/{firstApproved.Id}/refunds",
            new ExecuteRefundRequest(
                [new(visa.OriginalAllocationId, 300m, "VISA-REF")],
                "DETAIL\nNOTE"), firstRefundKey))
        {
            Assert.Equal(HttpStatusCode.Conflict, ambiguousReplay.StatusCode);
        }

        SecretaryResponse refundSecretary = await CreateSecretaryAsync(admin);
        _fixture.Clock.SetUtcNow(start.AddMinutes(-30));
        ShiftResponse refundShift = Assert.Single((await PostAndReadAsync<
            GenerateShiftsRequest, GeneratedShiftsResponse>(admin,
            "/api/admin/cashier/shifts/generate", new(refundSecretary.Id,
                clinicDate, clinicDate, [ClinicWeekday(clinicDate)],
                new TimeOnly(10, 0), new TimeOnly(14, 0)))).Items);
        _fixture.Clock.SetUtcNow(start);

        AppointmentVersionResponse secondAppointment =
            await GetAsync<AppointmentVersionResponse>(cashier,
                $"/api/appointments/{appointmentIds[1]}");
        ApprovalRequestResponse secondRequest = await PostAndReadAsync<
            CreateCancellationApprovalRequest, ApprovalRequestResponse>(cashier,
            "/api/cashier/approval-requests/appointment-cancellations",
            new(appointmentIds[1], secondAppointment.RowVersion, "طلب إلغاء ثان"));
        ApprovalRequestPageResponse approvalRequests =
            await GetAsync<ApprovalRequestPageResponse>(cashier,
                "/api/cashier/approval-requests?pageNumber=1&pageSize=20");
        Assert.Equal([secondRequest.Id, firstRequest.Id],
            approvalRequests.Items.Take(2).Select(item => item.Id));
        ApprovalRequestResponse secondApproved = await PostAndReadAsync<
            ReviewApprovalRequest, ApprovalRequestResponse>(admin,
            $"/api/admin/cashier/approval-requests/{secondRequest.Id}/approve",
            new(secondRequest.RowVersion, "اعتماد الاسترداد النقدي"));
        RefundableMethodResponse cash = Assert.Single(secondApproved.RefundableMethods,
            item => item.Code == "CASH");

        using HttpClient refundCashier = _fixture.CreateClient();
        await LoginAndChangePasswordAsync(refundCashier, refundSecretary.UserName,
            "SecretaryPass1", "SecretaryPass2");
        PaymentResponse patientPayment = await GetAsync<PaymentResponse>(refundCashier,
            $"/api/patients/{seeded.PatientId}/payments/{payment.Id}");
        Assert.Equal(payment.Id, patientPayment.Id);
        RefundResponse patientRefund = await GetAsync<RefundResponse>(refundCashier,
            $"/api/patients/{seeded.PatientId}/refunds/{firstRefund.Refund.Id}");
        Assert.Equal(firstRefund.Refund.Id, patientRefund.Id);
        ShiftResponse refundCurrent = await GetAsync<ShiftResponse>(refundCashier,
            "/api/cashier/shifts/current");
        Assert.Equal(refundShift.Id, refundCurrent.Id);
        await PostAndReadAsync<OpeningBalanceRequest, ShiftResponse>(refundCashier,
            $"/api/cashier/shifts/{refundCurrent.Id}/opening-balance",
            new(0m, refundCurrent.RowVersion));
        PostedRefundResponse secondRefund = await PostRefundAsync(refundCashier,
            secondApproved.Id,
            new([new(cash.OriginalAllocationId, 200m, null)], "استرداد نقدي"),
            Guid.NewGuid(), HttpStatusCode.Created);
        Assert.True(secondRefund.IsExpectedCashNegative);
        Assert.Equal(-200m, secondRefund.ExpectedCashAfterRefund);

        ShiftSummaryResponse refundSummary = await GetAsync<ShiftSummaryResponse>(
            refundCashier,
            $"/api/cashier/shifts/{refundShift.Id}/collection-summary");
        Assert.Equal(200m, refundSummary.CashRefunded);
        Assert.Equal(-200m, refundSummary.CashNet);
        Assert.Equal(-200m, refundSummary.CurrentExpectedCash);
        Assert.True(refundSummary.IsExpectedCashNegative);
        Assert.Equal(1, refundSummary.RefundCount);

        _fixture.Clock.SetUtcNow(start.AddHours(4));
        ShiftClosingResponse shiftToClose = await GetAsync<ShiftClosingResponse>(
            refundCashier, "/api/cashier/shifts/current");
        ShiftClosingResponse reconciled = await PostAndReadAsync<
            ReconcileShiftRequest, ShiftClosingResponse>(refundCashier,
            $"/api/cashier/shifts/{refundShift.Id}/reconcile",
            new(0m, shiftToClose.RowVersion));
        Assert.Equal(-200m, reconciled.ExpectedCash);
        Assert.Equal(200m, reconciled.CashVariance);
        ShiftClosingResponse closed = await PostAndReadAsync<
            CloseShiftRequest, ShiftClosingResponse>(refundCashier,
            $"/api/cashier/shifts/{refundShift.Id}/close",
            new(reconciled.RowVersion, "فرق ناتج عن استرداد نقدي"));
        Assert.Equal(ShiftStatus.Closed, closed.Status);
        Assert.Equal(-200m, closed.ExpectedCash);
        Assert.Equal(200m, closed.CashVariance);

        PaymentResponse refundedPayment = await GetAsync<PaymentResponse>(cashier,
            $"/api/cashier/payments/{payment.Id}");
        Assert.Equal(PaymentRecordStatus.Refunded, refundedPayment.Status);

        await using AsyncServiceScope finalScope =
            _fixture.Services.CreateAsyncScope();
        ClinicDbContext finalDb = finalScope.ServiceProvider
            .GetRequiredService<ClinicDbContext>();
        Assert.Equal(2, await finalDb.Refunds.CountAsync());
        Assert.All(await finalDb.Appointments.Where(item =>
            appointmentIds.Contains(item.Id)).ToArrayAsync(), item =>
        {
            Assert.Equal(PaymentStatus.Refunded, item.PaymentStatus);
            Assert.Equal(AppointmentStatus.Cancelled, item.Status);
        });
        Assert.Equal(2, await finalDb.AuditLogs.CountAsync(item =>
            item.Action == "cashier.refund_posted"));

        Patient archivedPatient = await finalDb.Patients.SingleAsync(item =>
            item.Id == seeded.PatientId);
        archivedPatient.Archive(seeded.AdminUserId, _fixture.Clock.GetUtcNow());
        await finalDb.SaveChangesAsync();

        using HttpResponseMessage archivedPatientPayment = await cashier.GetAsync(
            $"/api/patients/{seeded.PatientId}/payments/{payment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, archivedPatientPayment.StatusCode);
        PaymentResponse adminArchivedPatientPayment = await GetAsync<PaymentResponse>(admin,
            $"/api/admin/patients/{seeded.PatientId}/payments/{payment.Id}");
        Assert.Equal(payment.Id, adminArchivedPatientPayment.Id);

        using HttpResponseMessage archivedPatientRefund = await refundCashier.GetAsync(
            $"/api/patients/{seeded.PatientId}/refunds/{firstRefund.Refund.Id}");
        Assert.Equal(HttpStatusCode.NotFound, archivedPatientRefund.StatusCode);
        RefundResponse adminArchivedPatientRefund = await GetAsync<RefundResponse>(admin,
            $"/api/admin/patients/{seeded.PatientId}/refunds/{firstRefund.Refund.Id}");
        Assert.Equal(firstRefund.Refund.Id, adminArchivedPatientRefund.Id);
    }

    private async Task<SeededPaymentTargets> SeedPaymentTargetsAsync(DateOnly clinicDate,
        DateTimeOffset start)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        UserManager<ApplicationUser> users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser admin = await users.FindByNameAsync("integration.admin") ??
            throw new InvalidOperationException();
        DateTimeOffset now = _fixture.Clock.GetUtcNow();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Department department = Department.Create($"قسم الدفع {suffix}", null,
            "غرفة الدفع", now);
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        Specialization specialization = Specialization.Create(department.Id, "تخصص الدفع");
        db.Specializations.Add(specialization);
        await db.SaveChangesAsync();
        Service firstService = Service.Create(department.Id, specialization.Id,
            "خدمة أولى", ServiceType.Session, 30, PricingMode.Fixed, 300m,
            admin.Id, now);
        Service secondService = Service.Create(department.Id, specialization.Id,
            "خدمة ثانية", ServiceType.Session, 30, PricingMode.Fixed, 200m,
            admin.Id, now);
        Doctor doctor = Doctor.Create(department.Id, "د. الدفع", null, now);
        db.AddRange(firstService, secondService, doctor);
        await db.SaveChangesAsync();
        DoctorService firstAssignment = DoctorService.Create(doctor, firstService);
        DoctorService secondAssignment = DoctorService.Create(doctor, secondService);
        db.DoctorServices.AddRange(firstAssignment, secondAssignment);

        db.DoctorSchedules.Add(DoctorSchedule.Create(doctor.Id,
            ClinicWeekday(clinicDate), new TimeOnly(0, 0), new TimeOnly(23, 59),
            clinicDate, clinicDate));
        Patient patient = Patient.Create("مريض الدفع",
            $"010{Random.Shared.Next(10000000, 99999999):D8}", null, null, 30,
            PatientGender.Male, null, null, null, null, null, admin.Id,
            clinicDate, now);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        Appointment first = Appointment.Create(patient.Id, department.Room.Id,
            department.Id,
            start.AddHours(1), false, admin.Id, now);
        first.AddService(firstService.Id, firstAssignment.Id, 30, 1, 300m, []);
        Appointment second = Appointment.Create(patient.Id, department.Room.Id,
            department.Id,
            start.AddHours(2), false, admin.Id, now);
        second.AddService(secondService.Id, secondAssignment.Id, 30, 1, 200m, []);
        db.Appointments.AddRange(first, second);
        await db.SaveChangesAsync();
        Package package = Package.Create(department.Id, "باقة الدفع", 400m, 14, 90,
            [(firstService, 2, 200m), (secondService, 1, 200m)], admin.Id, now);
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        PatientPackage patientPackage = PatientPackage.Register(patient, package,
            Guid.NewGuid(), new string('A', 64), admin.Id, now);
        PatientPackage concurrentPatientPackage = PatientPackage.Register(patient, package,
            Guid.NewGuid(), new string('B', 64), admin.Id, now);
        db.PatientPackages.AddRange(patientPackage, concurrentPatientPackage);
        await db.SaveChangesAsync();
        return new([first.Id, second.Id], patientPackage.Id,
            concurrentPatientPackage.Id, patient.Id, department.Id, firstService.Id,
            secondService.Id, doctor.Id, admin.Id);
    }

    private async Task SetServiceActiveAsync(long serviceId, bool isActive)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ClinicDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [catalog].[Services]
            SET [IsActive] = {isActive}
            WHERE [Id] = {serviceId};
            """);
    }

    private async Task AddDepartmentClosureAsync(long departmentId, long adminUserId,
        DateTimeOffset startAt, DateTimeOffset endAt)
    {
        await using AsyncServiceScope scope = _fixture.Services.CreateAsyncScope();
        ClinicDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ClinicDbContext>();
        dbContext.DepartmentClosures.Add(DepartmentClosure.Create(departmentId,
            startAt, endAt, "اختبار إعادة جدولة جلسة باقة", adminUserId,
            _fixture.Clock.GetUtcNow()));
        await dbContext.SaveChangesAsync();
    }

    private async Task VerifyPackageAppointmentRetryAsync(SeededPaymentTargets seeded,
        DateTimeOffset start, long actorUserId)
    {
        string connectionString;
        await using (AsyncServiceScope scope = _fixture.Services.CreateAsyncScope())
        {
            ClinicDbContext current = scope.ServiceProvider
                .GetRequiredService<ClinicDbContext>();
            connectionString = current.Database.GetConnectionString() ??
                throw new InvalidOperationException("Missing test connection string.");
        }

        ThrowOnceOnPackageReservationInterceptor interceptor = new();
        DbContextOptions<ClinicDbContext> options =
            new DbContextOptionsBuilder<ClinicDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                    2, TimeSpan.Zero, null))
                .AddInterceptors(interceptor).Options;
        await using ClinicDbContext dbContext = new(options);
        AdjustableTimeProvider clock = new();
        clock.SetUtcNow(start);
        Clinic.Infrastructure.Appointments.AppointmentService service = new(dbContext, clock,
            new Clinic.Infrastructure.Discounts.DiscountResolver(dbContext));
        AppointmentInput input = new(seeded.PatientId, seeded.DepartmentId,
            start.AddHours(6), [new AppointmentLineInput(seeded.ServiceId,
                seeded.DoctorId, 1, [])], null, seeded.PatientPackageId, Guid.NewGuid());

        Result<AppointmentModel> created = await service.CreateAsync(actorUserId, input,
            CancellationToken.None);

        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Description : null);
        Assert.True(interceptor.WasTriggered);
        Result released = await service.MarkNoShowAsync(actorUserId, created.Value.Id,
            Convert.FromBase64String(created.Value.RowVersion), CancellationToken.None);
        Assert.True(released.IsSuccess);
    }

    private static async Task<SecretaryResponse> CreateSecretaryAsync(HttpClient admin)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        return await PostAndReadAsync<CreateSecretaryRequest, SecretaryResponse>(admin,
            "/api/admin/secretaries", new("سكرتيرة الدفع",
                $"010{Random.Shared.Next(10000000, 99999999):D8}", null,
                $"payment.secretary.{suffix}", "SecretaryPass1"));
    }

    private static DateTimeOffset ClinicInstant(DateOnly date, TimeOnly time)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        DateTime local = DateTime.SpecifyKind(date.ToDateTime(time),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static ClinicDayOfWeek ClinicWeekday(DateOnly date) =>
        (ClinicDayOfWeek)(((int)date.DayOfWeek + 6) % 7 + 1);

    private static async Task LoginAndChangePasswordAsync(HttpClient client,
        string userName, string currentPassword, string newPassword)
    {
        await LoginAsync(client, userName, currentPassword);
        using HttpResponseMessage change = await PostAsync(client,
            "/api/auth/change-password", new ChangePasswordRequest(currentPassword,
                newPassword));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string userName,
        string password)
    {
        using HttpResponseMessage login = await PostAsync(client, "/api/auth/login",
            new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task<TResponse> GetAsync<TResponse>(HttpClient client, string uri)
    {
        using HttpResponseMessage response = await client.GetAsync(uri);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<TResponse> PostAndReadAsync<TRequest, TResponse>(
        HttpClient client, string uri, TRequest request)
    {
        using HttpResponseMessage response = await PostAsync(client, uri, request);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<TResponse> PutAndReadAsync<TRequest, TResponse>(
        HttpClient client, string uri, TRequest request)
    {
        CsrfResponse? csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        using HttpRequestMessage message = new(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf?.Token);
        using HttpResponseMessage response = await client.SendAsync(message);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<TResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<HttpResponseMessage> PostPaymentAsync(HttpClient client,
        PostPaymentRequest request, Guid key)
    {
        HttpResponseMessage response = await CreatePostMessageAsync(client,
            "/api/cashier/payments", request, key);
        return response;
    }

    private static async Task<PostedRefundResponse> PostRefundAsync(HttpClient client,
        long requestId, ExecuteRefundRequest request, Guid key,
        HttpStatusCode expectedStatus)
    {
        using HttpResponseMessage response = await CreatePostMessageAsync(client,
            $"/api/cashier/approval-requests/{requestId}/refunds", request, key);
        string body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expectedStatus,
            $"{response.StatusCode}: {body}");
        return await response.Content.ReadFromJsonAsync<PostedRefundResponse>() ??
            throw new InvalidOperationException("Empty response.");
    }

    private static async Task<HttpResponseMessage> PostAsync<TRequest>(HttpClient client,
        string uri, TRequest request) => await CreatePostMessageAsync(client, uri,
            request, null);

    private static async Task<HttpResponseMessage> CreatePostMessageAsync<TRequest>(
        HttpClient client, string uri, TRequest request, Guid? key)
    {
        CsrfResponse? csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        using HttpRequestMessage message = new(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-XSRF-TOKEN", csrf?.Token);
        if (key.HasValue) message.Headers.Add("Idempotency-Key", key.Value.ToString());
        return await client.SendAsync(message);
    }

    private sealed record GenerateShiftsRequest(long SecretaryUserId, DateOnly FromDate,
        DateOnly ToDate, IReadOnlyCollection<ClinicDayOfWeek> DaysOfWeek,
        TimeOnly StartTime, TimeOnly EndTime);
    private sealed record GeneratedShiftsResponse(IReadOnlyCollection<ShiftResponse> Items);
    private sealed record ShiftResponse(long Id, DateTimeOffset GraceEndsAt,
        string RowVersion);
    private sealed record OpeningBalanceRequest(decimal Amount, string RowVersion);
    private sealed record ReconcileShiftRequest(decimal DeclaredCash,
        string RowVersion);
    private sealed record CloseShiftRequest(string RowVersion, string? Reason);
    private sealed record ShiftClosingResponse(long Id, decimal? ExpectedCash,
        decimal? DeclaredCash, decimal? CashVariance, ShiftStatus Status,
        string RowVersion);
    private sealed record PaymentMethodResponse(string Code);
    private sealed record ReportingComparisonResponse(
        IReadOnlyCollection<ReportingComparisonPoint> Points);
    private sealed record ReportingComparisonPoint(string Key, decimal Net,
        int AppointmentCount);
    private sealed record ReportingFinancialResponse(decimal Collected,
        IReadOnlyCollection<ReportingBreakdown> Departments,
        IReadOnlyCollection<ReportingBreakdown> PaymentMethods);
    private sealed record ReportingBreakdown(decimal Collected);
    private sealed record PaymentMethodAllocationRequest(long PaymentMethodId,
        decimal Amount, string? ReferenceNumber);
    private sealed record PostPaymentRequest(IReadOnlyCollection<long> AppointmentIds,
        IReadOnlyCollection<PaymentMethodAllocationRequest> MethodAllocations, string? Note,
        IReadOnlyCollection<long>? PatientPackageIds = null);
    private sealed record AppointmentPaymentAllocationResponse(long AppointmentId,
        decimal Amount);
    private sealed record PackagePaymentAllocationResponse(long PatientPackageId,
        string PackageName, decimal Amount);
    private sealed record PatientPackagePaymentReferenceResponse(long PaymentId,
        string TransactionNumber, DateTimeOffset CollectedAt);
    private sealed record PatientPackagePaymentResponse(
        PatientPackagePaymentStatus PaymentStatus,
        PatientPackagePaymentReferenceResponse? Payment, string RowVersion);
    private sealed record ExtendPatientPackageRequest(
        PatientPackageExtensionType ExtensionType, DateTimeOffset NewDeadline,
        string Reason, string RowVersion);
    private sealed record PackageBookingOptionsResponse(bool CanBook,
        IReadOnlyCollection<PackageBookingOptionServiceResponse> Services);
    private sealed record PackageBookingOptionServiceResponse(long ServiceId,
        int AvailableSessions, bool CanBook, string? UnavailabilityReason);
    private sealed record CreatePackageAppointmentRequest(long PatientId, long DepartmentId,
        DateTimeOffset StartAt, IReadOnlyCollection<PackageAppointmentLineRequest> Services,
        long PatientPackageId);
    private sealed record UpdatePackageAppointmentRequest(long PatientId, long DepartmentId,
        DateTimeOffset StartAt, IReadOnlyCollection<PackageAppointmentLineRequest> Services,
        string RowVersion, long PatientPackageId);
    private sealed record PackageAppointmentLineRequest(long ServiceId, long DoctorId,
        int Quantity, IReadOnlyCollection<long> OptionalDeviceIds);
    private sealed record PackageSessionBookingResponse(PackageSessionBookingStatus Status);
    private sealed record PackageAppointmentServiceResponse(
        PackageSessionBookingResponse? PackageSessionBooking);
    private sealed record PackageAppointmentResponse(long Id, AppointmentStatus Status,
        PaymentStatus PaymentStatus, decimal NetAmount, decimal PackageCoveredAmount,
        string RowVersion, IReadOnlyCollection<PackageAppointmentServiceResponse> Services);
    private sealed record AppointmentStateRequest(string RowVersion);
    private sealed record PaymentResponse(long Id, string TransactionNumber,
        decimal TotalAmount, PaymentRecordStatus Status,
        IReadOnlyCollection<AppointmentPaymentAllocationResponse> AppointmentAllocations,
        IReadOnlyCollection<PackagePaymentAllocationResponse> PackageAllocations);
    private sealed record ShiftSummaryResponse(decimal CashCollected,
        decimal CashRefunded, decimal CashNet, decimal ElectronicCollected,
        decimal TotalCollected, decimal? CurrentExpectedCash,
        bool IsExpectedCashNegative, int PaymentCount, int RefundCount);
    private sealed record AppointmentVersionResponse(string RowVersion);
    private sealed record CreateCancellationApprovalRequest(long AppointmentId,
        string AppointmentRowVersion, string Reason);
    private sealed record ReviewApprovalRequest(string RowVersion, string Reason);
    private sealed record RefundableMethodResponse(long OriginalAllocationId,
        string Code, decimal RemainingAmount);
    private sealed record ApprovalRequestResponse(long Id, decimal? RequestedAmount,
        IReadOnlyCollection<RefundableMethodResponse> RefundableMethods,
        string RowVersion);
    private sealed record ApprovalRequestPageResponse(
        IReadOnlyCollection<ApprovalRequestResponse> Items);
    private sealed record RefundMethodRequest(long OriginalAllocationId,
        decimal Amount, string? ReferenceNumber);
    private sealed record ExecuteRefundRequest(
        IReadOnlyCollection<RefundMethodRequest> MethodAllocations, string? Note);
    private sealed record RefundResponse(long Id, decimal Amount);
    private sealed record PatientTimelineResponse(
        IReadOnlyCollection<PatientTimelineItemResponse> Items);
    private sealed record PatientTimelineItemResponse(PatientTimelineRecordType RecordType);
    private sealed record PostedRefundResponse(RefundResponse Refund, bool WasReplayed,
        decimal ExpectedCashAfterRefund, bool IsExpectedCashNegative);
    private sealed record CreateSecretaryRequest(string FullName, string PhoneNumber,
        string? Email, string UserName, string TemporaryPassword);
    private sealed record SecretaryResponse(long Id, string UserName);
    private sealed record LoginRequest(string UserName, string Password);
    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private sealed record CsrfResponse(string Token);
    private sealed record SeededPaymentTargets(long[] AppointmentIds,
        long PatientPackageId, long ConcurrentPatientPackageId, long PatientId,
        long DepartmentId, long ServiceId, long SecondServiceId, long DoctorId,
        long AdminUserId);

    private sealed class ThrowOnceOnPackageReservationInterceptor : SaveChangesInterceptor
    {
        public bool WasTriggered { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!WasTriggered && eventData.Context is not null &&
                eventData.Context.ChangeTracker.Entries<PackageSessionBooking>()
                    .Any(item => item.State == EntityState.Added))
            {
                WasTriggered = true;
                throw new TimeoutException("Transient package reservation failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
