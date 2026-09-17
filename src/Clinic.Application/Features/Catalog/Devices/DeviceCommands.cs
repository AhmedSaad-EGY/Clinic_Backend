namespace Clinic.Application.Features.Catalog.Devices;

public sealed record CreateDeviceCommand(
    long DepartmentId,
    string Name,
    string? Identifier) : ICommand<DeviceModel>;

public sealed class CreateDeviceCommandHandler
    : ICommandHandler<CreateDeviceCommand, DeviceModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IDeviceCatalogService _service;

    public CreateDeviceCommandHandler(ICurrentUser currentUser, IDeviceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<DeviceModel>> Handle(
        CreateDeviceCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result name = CatalogCommandValidation.ValidateName(command.Name, "اسم الجهاز", 200);
        Result identifier = CatalogCommandValidation.ValidateOptionalText(
            command.Identifier,
            "رقم الجهاز",
            100);
        if (actor.IsFailure || name.IsFailure || identifier.IsFailure || command.DepartmentId <= 0)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : name.IsFailure
                    ? name.Error
                    : identifier.IsFailure
                        ? identifier.Error
                    : CatalogErrors.Validation("القسم غير صحيح.");
            return Task.FromResult(Result.Failure<DeviceModel>(error));
        }

        return _service.CreateAsync(
            actor.Value,
            command.DepartmentId,
            command.Name.Trim(),
            string.IsNullOrWhiteSpace(command.Identifier) ? null : command.Identifier.Trim(),
            cancellationToken);
    }
}

public sealed record UpdateDeviceCommand(
    long DeviceId,
    string Name,
    string? Identifier,
    string RowVersion) : ICommand<DeviceModel>;

public sealed class UpdateDeviceCommandHandler
    : ICommandHandler<UpdateDeviceCommand, DeviceModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IDeviceCatalogService _service;

    public UpdateDeviceCommandHandler(ICurrentUser currentUser, IDeviceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<DeviceModel>> Handle(
        UpdateDeviceCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result<byte[]> version = CatalogCommandValidation.DecodeRowVersion(command.RowVersion);
        Result name = CatalogCommandValidation.ValidateName(command.Name, "اسم الجهاز", 200);
        Result identifier = CatalogCommandValidation.ValidateOptionalText(
            command.Identifier,
            "رقم الجهاز",
            100);
        if (actor.IsFailure || version.IsFailure || name.IsFailure || identifier.IsFailure)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : version.IsFailure
                    ? version.Error
                    : name.IsFailure ? name.Error : identifier.Error;
            return Task.FromResult(Result.Failure<DeviceModel>(error));
        }

        return _service.UpdateAsync(
            actor.Value,
            command.DeviceId,
            command.Name.Trim(),
            string.IsNullOrWhiteSpace(command.Identifier) ? null : command.Identifier.Trim(),
            version.Value,
            cancellationToken);
    }
}

public sealed record ArchiveDeviceCommand(long DeviceId, string RowVersion) : ICommand;

public sealed class ArchiveDeviceCommandHandler : ICommandHandler<ArchiveDeviceCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IDeviceCatalogService _service;

    public ArchiveDeviceCommandHandler(ICurrentUser currentUser, IDeviceCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(
        ArchiveDeviceCommand command,
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
            command.DeviceId,
            version.Value,
            cancellationToken);
    }
}
