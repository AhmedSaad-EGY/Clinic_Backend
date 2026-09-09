using Clinic.Api.Contracts.Appointments;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Appointments;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Features.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    [HttpPost("availability")]
    public async Task<ActionResult<AppointmentAvailability>> Availability(
        CheckAppointmentAvailabilityRequest request,
        CheckAppointmentAvailabilityQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new CheckAppointmentAvailabilityQuery(
            Map(request.PatientId, request.DepartmentId, request.StartAt, request.Services),
            request.ExcludedAppointmentId), token));

    [HttpPost]
    public async Task<ActionResult<AppointmentModel>> Create(CreateAppointmentRequest request,
        CreateAppointmentCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new CreateAppointmentCommand(
            Map(request.PatientId, request.DepartmentId, request.StartAt, request.Services,
                request.FollowUp)), token));

    [HttpPut("{appointmentId:long}")]
    public async Task<ActionResult<AppointmentModel>> Update(long appointmentId,
        UpdateAppointmentRequest request, UpdateAppointmentCommandHandler handler,
        CancellationToken token) => this.ToActionResult(await handler.Handle(
            new UpdateAppointmentCommand(appointmentId,
                Map(request.PatientId, request.DepartmentId, request.StartAt, request.Services),
                request.RowVersion), token));

    [HttpGet("{appointmentId:long}")]
    public async Task<ActionResult<AppointmentModel>> Get(long appointmentId,
        GetAppointmentQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetAppointmentQuery(appointmentId), token));

    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyCollection<AppointmentModel>>> Calendar(
        [FromQuery] DateOnly date, [FromQuery] long? departmentId,
        GetAppointmentCalendarQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new GetAppointmentCalendarQuery(date, departmentId), token));

    [HttpPost("{appointmentId:long}/cancel")]
    public Task<ActionResult> Cancel(long appointmentId, ChangeAppointmentStateRequest request,
        ChangeAppointmentStateCommandHandler handler, CancellationToken token) =>
        ChangeState(appointmentId, AppointmentStateAction.Cancel, request, handler, token);

    [HttpPost("{appointmentId:long}/complete")]
    public Task<ActionResult> Complete(long appointmentId, ChangeAppointmentStateRequest request,
        ChangeAppointmentStateCommandHandler handler, CancellationToken token) =>
        ChangeState(appointmentId, AppointmentStateAction.Complete, request, handler, token);

    [HttpPost("{appointmentId:long}/no-show")]
    public Task<ActionResult> NoShow(long appointmentId, ChangeAppointmentStateRequest request,
        ChangeAppointmentStateCommandHandler handler, CancellationToken token) =>
        ChangeState(appointmentId, AppointmentStateAction.NoShow, request, handler, token);

    private async Task<ActionResult> ChangeState(long appointmentId,
        AppointmentStateAction action, ChangeAppointmentStateRequest request,
        ChangeAppointmentStateCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ChangeAppointmentStateCommand(
            appointmentId, action, request.Reason, request.RowVersion), token));

    internal static AppointmentInput Map(long patientId, long departmentId,
        DateTimeOffset startAt, IReadOnlyCollection<AppointmentLineRequest> services,
        FollowUpBookingRequest? followUp = null) =>
        new(patientId, departmentId, startAt, services.Select(item =>
            new AppointmentLineInput(item.ServiceId, item.DoctorId, item.Quantity,
                item.OptionalDeviceIds ?? [])).ToArray(), followUp is null ? null
                    : new FollowUpBookingInput(followUp.FollowUpId, followUp.RowVersion));
}
