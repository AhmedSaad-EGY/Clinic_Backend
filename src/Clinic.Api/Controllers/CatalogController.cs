using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Catalog.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.PasswordChanged)]
[Route("api/catalog")]
public sealed class CatalogController : ControllerBase
{
    [HttpGet("departments")]
    public async Task<ActionResult<CatalogPage<DepartmentModel>>> Departments(
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListDepartmentsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<DepartmentModel>> result = await handler.Handle(
            new ListDepartmentsQuery(
                IncludeArchived: false,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 50 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("departments/{departmentId:long}/specializations")]
    public async Task<ActionResult<CatalogPage<SpecializationModel>>> Specializations(
        long departmentId,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListSpecializationsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<SpecializationModel>> result = await handler.Handle(
            new ListSpecializationsQuery(
                departmentId,
                IncludeArchived: false,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 50 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("departments/{departmentId:long}/devices")]
    public async Task<ActionResult<CatalogPage<DeviceModel>>> Devices(
        long departmentId,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListDevicesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<DeviceModel>> result = await handler.Handle(
            new ListDevicesQuery(
                departmentId,
                IncludeArchived: false,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 50 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("specializations/{specializationId:long}/services")]
    public async Task<ActionResult<CatalogPage<ServiceModel>>> Services(
        long specializationId,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListServicesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<ServiceModel>> result = await handler.Handle(
            new ListServicesQuery(
                specializationId,
                IncludeArchived: false,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 50 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("services/{serviceId:long}")]
    public async Task<ActionResult<ServiceModel>> Service(
        long serviceId,
        GetServiceQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<ServiceModel> result = await handler.Handle(
            new GetServiceQuery(serviceId, IncludeArchived: false),
            cancellationToken);
        return this.ToActionResult(result);
    }
}
