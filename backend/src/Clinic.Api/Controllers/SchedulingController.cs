using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Scheduling;
using Clinic.Application.Features.Scheduling.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/scheduling")]
public sealed class SchedulingController : ControllerBase
{
    [HttpGet("doctors")]
    public async Task<ActionResult<SchedulingPage<DoctorModel>>> Doctors(
        [FromQuery] long? departmentId, [FromQuery] long? serviceId,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListDoctorsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new ListDoctorsQuery(
                departmentId,
                serviceId,
                false,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize), token));

    [HttpGet("services/{serviceId:long}/doctors")]
    public async Task<ActionResult<SchedulingPage<DoctorModel>>> ServiceDoctors(
        long serviceId, [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListDoctorsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new ListDoctorsQuery(
                null,
                serviceId,
                false,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize), token));

    [HttpGet("doctors/{doctorId:long}/schedules")]
    public async Task<ActionResult<SchedulingPage<DoctorScheduleModel>>> Schedules(
        long doctorId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListDoctorSchedulesQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new ListDoctorSchedulesQuery(
                doctorId,
                from,
                to,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize), token));

    [HttpGet("doctors/{doctorId:long}/exceptions")]
    public async Task<ActionResult<SchedulingPage<DoctorExceptionModel>>> Exceptions(
        long doctorId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] int pageNumber, [FromQuery] int pageSize,
        ListDoctorExceptionsQueryHandler handler, CancellationToken token) =>
        this.ToActionResult(await handler.Handle(
            new ListDoctorExceptionsQuery(
                doctorId,
                from,
                to,
                false,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize), token));

    [HttpGet("departments/{departmentId:long}/availability")]
    public async Task<ActionResult<DepartmentAvailabilityModel>> DepartmentAvailability(
        long departmentId, [FromQuery] DateTimeOffset? at,
        GetDepartmentAvailabilityQueryHandler handler, TimeProvider timeProvider,
        CancellationToken token) =>
        this.ToActionResult(await handler.Handle(new GetDepartmentAvailabilityQuery(
            departmentId, at ?? timeProvider.GetUtcNow()), token));
}
