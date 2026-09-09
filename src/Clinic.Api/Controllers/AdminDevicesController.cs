using Clinic.Api.Contracts.Catalog;
using Clinic.Api.Infrastructure.Errors;
using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Features.Catalog.Devices;
using Clinic.Application.Features.Catalog.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin")]
public sealed class AdminDevicesController : ControllerBase
{
    [HttpGet("departments/{departmentId:long}/devices")]
    public async Task<ActionResult<CatalogPage<DeviceModel>>> List(
        long departmentId,
        [FromQuery] bool includeArchived,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListDevicesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<DeviceModel>> result = await handler.Handle(
            new ListDevicesQuery(
                departmentId,
                includeArchived,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("departments/{departmentId:long}/devices")]
    public async Task<ActionResult<DeviceModel>> Create(
        long departmentId,
        CreateDeviceRequest request,
        CreateDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<DeviceModel> result = await handler.Handle(
            new CreateDeviceCommand(departmentId, request.Name, request.Identifier),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("devices/{deviceId:long}")]
    public async Task<ActionResult<DeviceModel>> Update(
        long deviceId,
        UpdateDeviceRequest request,
        UpdateDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<DeviceModel> result = await handler.Handle(
            new UpdateDeviceCommand(
                deviceId,
                request.Name,
                request.Identifier,
                request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("devices/{deviceId:long}/archive")]
    public async Task<IActionResult> Archive(
        long deviceId,
        RowVersionRequest request,
        ArchiveDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new ArchiveDeviceCommand(deviceId, request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }
}
