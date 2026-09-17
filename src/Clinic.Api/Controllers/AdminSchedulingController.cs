namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/scheduling")]
public sealed class AdminSchedulingController : ControllerBase
{
    [HttpGet("doctors/{doctorId:long}/schedules")]
    public async Task<ActionResult<SchedulingPage<DoctorScheduleModel>>> Schedules(
        long doctorId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListDoctorSchedulesQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ListDoctorSchedulesQuery(
            doctorId,
            from,
            to,
            pageNumber == 0 ? 1 : pageNumber,
            pageSize == 0 ? 20 : pageSize), token));

    [HttpPost("doctors/{doctorId:long}/schedules")]
    public async Task<ActionResult<DoctorScheduleModel>> CreateSchedule(
        long doctorId, CreateDoctorScheduleRequest request,
        CreateDoctorScheduleCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new CreateDoctorScheduleCommand(
            doctorId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.EffectiveFrom, request.EffectiveTo), token));

    [HttpPut("schedules/{scheduleId:long}")]
    public async Task<ActionResult<DoctorScheduleModel>> UpdateSchedule(
        long scheduleId, UpdateDoctorScheduleRequest request,
        UpdateDoctorScheduleCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new UpdateDoctorScheduleCommand(
            scheduleId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.EffectiveFrom, request.EffectiveTo, request.RowVersion,
            request.ConfirmAffectedAppointments), token));

    [HttpPost("schedules/{scheduleId:long}/deactivate")]
    public async Task<IActionResult> DeactivateSchedule(
        long scheduleId, SchedulingRowVersionRequest request,
        DeactivateDoctorScheduleCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new DeactivateDoctorScheduleCommand(scheduleId, request.RowVersion,
                request.ConfirmAffectedAppointments), token));

    [HttpGet("doctors/{doctorId:long}/exceptions")]
    public async Task<ActionResult<SchedulingPage<DoctorExceptionModel>>> Exceptions(
        long doctorId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] bool includeCancelled, [FromQuery] int pageNumber,
        [FromQuery] int pageSize, ListDoctorExceptionsQueryHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ListDoctorExceptionsQuery(
            doctorId,
            from,
            to,
            includeCancelled,
            pageNumber == 0 ? 1 : pageNumber,
            pageSize == 0 ? 20 : pageSize), token));

    [HttpPost("doctors/{doctorId:long}/exceptions")]
    public async Task<ActionResult<DoctorExceptionModel>> CreateException(
        long doctorId, CreateDoctorExceptionRequest request,
        CreateDoctorExceptionCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new CreateDoctorExceptionCommand(
            doctorId, request.Date, request.StartTime, request.EndTime,
            request.Type, request.Reason, request.ConfirmAffectedAppointments), token));

    [HttpPost("exceptions/{exceptionId:long}/cancel")]
    public async Task<IActionResult> CancelException(
        long exceptionId, SchedulingRowVersionRequest request,
        CancelDoctorExceptionCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new CancelDoctorExceptionCommand(exceptionId, request.RowVersion), token));

    [HttpGet("departments/{departmentId:long}/closures")]
    public async Task<ActionResult<SchedulingPage<DepartmentClosureModel>>> Closures(
        long departmentId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] bool includeCancelled, [FromQuery] int pageNumber,
        [FromQuery] int pageSize, ListDepartmentClosuresQueryHandler handler,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new ListDepartmentClosuresQuery(
            departmentId,
            from,
            to,
            includeCancelled,
            pageNumber == 0 ? 1 : pageNumber,
            pageSize == 0 ? 20 : pageSize), token));

    [HttpPost("departments/{departmentId:long}/closures")]
    public async Task<ActionResult<DepartmentClosureModel>> CreateClosure(
        long departmentId, CreateDepartmentClosureRequest request,
        CreateDepartmentClosureCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new CreateDepartmentClosureCommand(
            departmentId, request.StartAt, request.EndAt, request.Reason,
            request.ConfirmAffectedAppointments), token));

    [HttpPost("closures/{closureId:long}/cancel")]
    public async Task<IActionResult> CancelClosure(
        long closureId, SchedulingRowVersionRequest request,
        CancelDepartmentClosureCommandHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new CancelDepartmentClosureCommand(closureId, request.RowVersion), token));
}
