namespace Clinic.Application.Features.Catalog.Departments;

public sealed record CreateDepartmentCommand(
    string Name,
    string? Description,
    string RoomName) : ICommand<DepartmentModel>;

public sealed class CreateDepartmentCommandHandler
    : ICommandHandler<CreateDepartmentCommand, DepartmentModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IDepartmentCatalogService _service;

    public CreateDepartmentCommandHandler(
        ICurrentUser currentUser,
        IDepartmentCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<DepartmentModel>> Handle(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result name = CatalogCommandValidation.ValidateName(command.Name, "اسم القسم", 150);
        Result roomName = CatalogCommandValidation.ValidateName(
            command.RoomName,
            "اسم الغرفة",
            150);

        if (actor.IsFailure || name.IsFailure || roomName.IsFailure)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : name.IsFailure ? name.Error : roomName.Error;
            return Task.FromResult(Result.Failure<DepartmentModel>(error));
        }

        Result description = CatalogCommandValidation.ValidateOptionalText(
            command.Description,
            "وصف القسم",
            1000);
        if (description.IsFailure)
        {
            return Task.FromResult(Result.Failure<DepartmentModel>(
                description.Error));
        }

        return _service.CreateAsync(
            actor.Value,
            command.Name.Trim(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            command.RoomName.Trim(),
            cancellationToken);
    }
}

public sealed record UpdateDepartmentCommand(
    long DepartmentId,
    string Name,
    string? Description,
    string RoomName,
    string RowVersion) : ICommand<DepartmentModel>;

public sealed class UpdateDepartmentCommandHandler
    : ICommandHandler<UpdateDepartmentCommand, DepartmentModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IDepartmentCatalogService _service;

    public UpdateDepartmentCommandHandler(
        ICurrentUser currentUser,
        IDepartmentCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<DepartmentModel>> Handle(
        UpdateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result<byte[]> version = CatalogCommandValidation.DecodeRowVersion(command.RowVersion);
        Result name = CatalogCommandValidation.ValidateName(command.Name, "اسم القسم", 150);
        Result roomName = CatalogCommandValidation.ValidateName(
            command.RoomName,
            "اسم الغرفة",
            150);
        Result description = CatalogCommandValidation.ValidateOptionalText(
            command.Description,
            "وصف القسم",
            1000);

        if (actor.IsFailure || version.IsFailure || name.IsFailure ||
            roomName.IsFailure || description.IsFailure)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : version.IsFailure
                    ? version.Error
                    : name.IsFailure
                        ? name.Error
                        : roomName.IsFailure ? roomName.Error : description.Error;
            return Task.FromResult(Result.Failure<DepartmentModel>(error));
        }

        return _service.UpdateAsync(
            actor.Value,
            command.DepartmentId,
            command.Name.Trim(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            command.RoomName.Trim(),
            version.Value,
            cancellationToken);
    }
}

public sealed record ArchiveDepartmentCommand(
    long DepartmentId,
    string RowVersion) : ICommand;

public sealed class ArchiveDepartmentCommandHandler
    : ICommandHandler<ArchiveDepartmentCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IDepartmentCatalogService _service;

    public ArchiveDepartmentCommandHandler(
        ICurrentUser currentUser,
        IDepartmentCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(
        ArchiveDepartmentCommand command,
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
            command.DepartmentId,
            version.Value,
            cancellationToken);
    }
}
