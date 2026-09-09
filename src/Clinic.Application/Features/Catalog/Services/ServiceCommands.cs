using Clinic.Application.Abstractions.Catalog;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using Clinic.Domain.Catalog;

namespace Clinic.Application.Features.Catalog.Services;

public sealed record CreateServiceCommand(
    long DepartmentId,
    long SpecializationId,
    string Name,
    ServiceType ServiceType,
    int DurationMinutes,
    PricingMode PricingMode,
    decimal UnitPrice) : ICommand<ServiceModel>;

public sealed class CreateServiceCommandHandler
    : ICommandHandler<CreateServiceCommand, ServiceModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IServiceCatalogService _service;

    public CreateServiceCommandHandler(ICurrentUser currentUser, IServiceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<ServiceModel>> Handle(
        CreateServiceCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result validation = ValidateDetails(
            command.DepartmentId,
            command.SpecializationId,
            command.Name,
            command.ServiceType,
            command.DurationMinutes,
            command.PricingMode,
            command.UnitPrice);

        if (actor.IsFailure || validation.IsFailure)
        {
            return Task.FromResult(Result.Failure<ServiceModel>(
                actor.IsFailure ? actor.Error : validation.Error));
        }

        return _service.CreateAsync(
            actor.Value,
            command.DepartmentId,
            command.SpecializationId,
            command.Name.Trim(),
            command.ServiceType,
            command.DurationMinutes,
            command.PricingMode,
            command.UnitPrice,
            cancellationToken);
    }

    internal static Result ValidateDetails(
        long departmentId,
        long specializationId,
        string name,
        ServiceType serviceType,
        int durationMinutes,
        PricingMode pricingMode,
        decimal unitPrice)
    {
        Result nameResult = CatalogCommandValidation.ValidateName(name, "اسم الخدمة", 200);
        if (nameResult.IsFailure)
        {
            return nameResult;
        }

        if (departmentId <= 0 || specializationId <= 0)
        {
            return Result.Failure(CatalogErrors.Validation(
                "القسم والتخصص مطلوبان."));
        }

        if (!Enum.IsDefined(serviceType) || !Enum.IsDefined(pricingMode))
        {
            return Result.Failure(CatalogErrors.Validation(
                "نوع الخدمة أو طريقة التسعير غير صحيحة."));
        }

        if (durationMinutes is <= 0 or > 1440 || unitPrice <= 0)
        {
            return Result.Failure(CatalogErrors.Validation(
                "مدة الخدمة والسعر يجب أن يكونا أكبر من صفر."));
        }

        return Result.Success();
    }
}

public sealed record UpdateServiceCommand(
    long ServiceId,
    string Name,
    ServiceType ServiceType,
    int DurationMinutes,
    PricingMode PricingMode,
    string RowVersion) : ICommand<ServiceModel>;

public sealed class UpdateServiceCommandHandler
    : ICommandHandler<UpdateServiceCommand, ServiceModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IServiceCatalogService _service;

    public UpdateServiceCommandHandler(ICurrentUser currentUser, IServiceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<ServiceModel>> Handle(
        UpdateServiceCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result<byte[]> version = CatalogCommandValidation.DecodeRowVersion(command.RowVersion);
        Result name = CatalogCommandValidation.ValidateName(command.Name, "اسم الخدمة", 200);

        if (actor.IsFailure || version.IsFailure || name.IsFailure)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : version.IsFailure ? version.Error : name.Error;
            return Task.FromResult(Result.Failure<ServiceModel>(error));
        }

        if (!Enum.IsDefined(command.ServiceType) ||
            !Enum.IsDefined(command.PricingMode) ||
            command.DurationMinutes is <= 0 or > 1440)
        {
            return Task.FromResult(Result.Failure<ServiceModel>(
                CatalogErrors.Validation("بيانات الخدمة غير صحيحة.")));
        }

        return _service.UpdateAsync(
            actor.Value,
            command.ServiceId,
            command.Name.Trim(),
            command.ServiceType,
            command.DurationMinutes,
            command.PricingMode,
            version.Value,
            cancellationToken);
    }
}

public sealed record ChangeServicePriceCommand(
    long ServiceId,
    decimal UnitPrice,
    string RowVersion) : ICommand<ServiceModel>;

public sealed class ChangeServicePriceCommandHandler
    : ICommandHandler<ChangeServicePriceCommand, ServiceModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IServiceCatalogService _service;

    public ChangeServicePriceCommandHandler(
        ICurrentUser currentUser,
        IServiceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<ServiceModel>> Handle(
        ChangeServicePriceCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result<byte[]> version = CatalogCommandValidation.DecodeRowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.UnitPrice <= 0)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : version.IsFailure
                    ? version.Error
                    : CatalogErrors.Validation("السعر يجب أن يكون أكبر من صفر.");
            return Task.FromResult(Result.Failure<ServiceModel>(error));
        }

        return _service.ChangePriceAsync(
            actor.Value,
            command.ServiceId,
            command.UnitPrice,
            version.Value,
            cancellationToken);
    }
}

public sealed record ReplaceServiceDevicesCommand(
    long ServiceId,
    IReadOnlyCollection<ServiceDeviceInput> Devices,
    string RowVersion) : ICommand<ServiceModel>;

public sealed class ReplaceServiceDevicesCommandHandler
    : ICommandHandler<ReplaceServiceDevicesCommand, ServiceModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IServiceCatalogService _service;

    public ReplaceServiceDevicesCommandHandler(
        ICurrentUser currentUser,
        IServiceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<ServiceModel>> Handle(
        ReplaceServiceDevicesCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result<byte[]> version = CatalogCommandValidation.DecodeRowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure<ServiceModel>(
                actor.IsFailure ? actor.Error : version.Error));
        }

        if (command.Devices is null ||
            command.Devices.Any(item => item.DeviceId <= 0) ||
            command.Devices.Select(item => item.DeviceId).Distinct().Count() != command.Devices.Count)
        {
            return Task.FromResult(Result.Failure<ServiceModel>(
                CatalogErrors.Validation("قائمة الأجهزة غير صحيحة أو تحتوي على تكرار.")));
        }

        return _service.ReplaceDevicesAsync(
            actor.Value,
            command.ServiceId,
            command.Devices,
            version.Value,
            cancellationToken);
    }
}

public sealed record ArchiveServiceCommand(long ServiceId, string RowVersion) : ICommand;

public sealed class ArchiveServiceCommandHandler : ICommandHandler<ArchiveServiceCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IServiceCatalogService _service;

    public ArchiveServiceCommandHandler(ICurrentUser currentUser, IServiceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(
        ArchiveServiceCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result<byte[]> version = CatalogCommandValidation.DecodeRowVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure(
                actor.IsFailure ? actor.Error : version.Error));
        }

        return _service.ArchiveAsync(
            actor.Value,
            command.ServiceId,
            version.Value,
            cancellationToken);
    }
}
