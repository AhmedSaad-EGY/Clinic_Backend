namespace Clinic.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicyNames.AdminOnly)]
[Route("api/admin/services")]
public sealed class AdminServicesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CatalogPage<ServiceModel>>> List(
        [FromQuery] long specializationId,
        [FromQuery] bool includeArchived,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListServicesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<ServiceModel>> result = await handler.Handle(
            new ListServicesQuery(
                specializationId,
                includeArchived,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{serviceId:long}")]
    public async Task<ActionResult<ServiceModel>> Get(
        long serviceId,
        GetServiceQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<ServiceModel> result = await handler.Handle(
            new GetServiceQuery(serviceId, IncludeArchived: true),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<ServiceModel>> Create(
        CreateServiceRequest request,
        CreateServiceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<ServiceModel> result = await handler.Handle(
            new CreateServiceCommand(
                request.DepartmentId,
                request.SpecializationId,
                request.Name,
                request.ServiceType,
                request.DurationMinutes,
                request.PricingMode,
                request.UnitPrice),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{serviceId:long}")]
    public async Task<ActionResult<ServiceModel>> Update(
        long serviceId,
        UpdateServiceRequest request,
        UpdateServiceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<ServiceModel> result = await handler.Handle(
            new UpdateServiceCommand(
                serviceId,
                request.Name,
                request.ServiceType,
                request.DurationMinutes,
                request.PricingMode,
                request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{serviceId:long}/price")]
    public async Task<ActionResult<ServiceModel>> ChangePrice(
        long serviceId,
        ChangeServicePriceRequest request,
        ChangeServicePriceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result<ServiceModel> result = await handler.Handle(
            new ChangeServicePriceCommand(serviceId, request.UnitPrice, request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{serviceId:long}/prices")]
    public async Task<ActionResult<CatalogPage<ServicePriceHistoryModel>>> PriceHistory(
        long serviceId,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        ListServicePriceHistoryQueryHandler handler,
        CancellationToken cancellationToken)
    {
        Result<CatalogPage<ServicePriceHistoryModel>> result = await handler.Handle(
            new ListServicePriceHistoryQuery(
                serviceId,
                pageNumber == 0 ? 1 : pageNumber,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("{serviceId:long}/devices")]
    public async Task<ActionResult<ServiceModel>> ReplaceDevices(
        long serviceId,
        ReplaceServiceDevicesRequest request,
        ReplaceServiceDevicesCommandHandler handler,
        CancellationToken cancellationToken)
    {
        ServiceDeviceInput[] devices = request.Devices?
            .Select(item => new ServiceDeviceInput(item.DeviceId, item.IsRequired))
            .ToArray() ?? [];
        Result<ServiceModel> result = await handler.Handle(
            new ReplaceServiceDevicesCommand(serviceId, devices, request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{serviceId:long}/archive")]
    public async Task<IActionResult> Archive(
        long serviceId,
        RowVersionRequest request,
        ArchiveServiceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        Result result = await handler.Handle(
            new ArchiveServiceCommand(serviceId, request.RowVersion),
            cancellationToken);
        return this.ToActionResult(result);
    }
}
