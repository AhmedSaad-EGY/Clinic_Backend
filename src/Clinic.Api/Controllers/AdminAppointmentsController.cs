namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/appointments")]
public sealed class AdminAppointmentsController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AppointmentModel>> Create(
        AdminCreateAppointmentRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        CreateAppointmentCommandHandler handler, CancellationToken token)
    {
        Result<AppointmentModel> result = await handler.Handle(new CreateAppointmentCommand(
            AppointmentsController.Map(request.PatientId, request.DepartmentId,
                request.StartAt, request.Services, request.FollowUp,
                request.PatientPackageId, idempotencyKey, request.DiscountOverride)), token);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        if (result.Value.WasReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(result.Value);
        }

        return Created($"/api/appointments/{result.Value.Id}", result.Value);
    }

    [HttpGet]
    public async Task<ActionResult<AppointmentPage>> Search(
        [FromQuery] AppointmentSearchRequest request,
        SearchAppointmentsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new SearchAppointmentsQuery(
            new AppointmentSearch(request.From, request.To, request.DepartmentId,
                request.SpecializationId, request.ServiceId, request.DoctorId,
                request.Status, request.PaymentStatus, request.CreatedByUserId,
                request.PatientSearch, request.MinimumAge, request.MaximumAge,
                request.Gender, request.Area, request.PageNumber, request.PageSize)), token));

    [HttpPost("suspended/revalidate")]
    public async Task<ActionResult<int>> Revalidate([FromQuery] long? departmentId,
        RevalidateSuspendedAppointmentsCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new RevalidateSuspendedAppointmentsCommand(departmentId), token));

    [HttpPost("{appointmentId:long}/services/{appointmentServiceId:long}/transfer-doctor")]
    public async Task<ActionResult<AppointmentModel>> TransferDoctor(long appointmentId,
        long appointmentServiceId, TransferAppointmentDoctorRequest request,
        TransferAppointmentDoctorCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new TransferAppointmentDoctorCommand(
            appointmentId, appointmentServiceId, request.DoctorId, request.Reason,
            request.RowVersion), token));
}
