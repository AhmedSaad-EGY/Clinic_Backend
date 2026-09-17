namespace Clinic.Application.Features.Catalog.Specializations;

public sealed record CreateSpecializationCommand(
    long DepartmentId,
    string Name) : ICommand<SpecializationModel>;

public sealed class CreateSpecializationCommandHandler
    : ICommandHandler<CreateSpecializationCommand, SpecializationModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISpecializationCatalogService _service;

    public CreateSpecializationCommandHandler(
        ICurrentUser currentUser,
        ISpecializationCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<SpecializationModel>> Handle(
        CreateSpecializationCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result name = CatalogCommandValidation.ValidateName(command.Name, "اسم التخصص", 150);
        if (actor.IsFailure || name.IsFailure || command.DepartmentId <= 0)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : name.IsFailure
                    ? name.Error
                    : CatalogErrors.Validation("القسم غير صحيح.");
            return Task.FromResult(Result.Failure<SpecializationModel>(error));
        }

        return _service.CreateAsync(
            actor.Value,
            command.DepartmentId,
            command.Name.Trim(),
            cancellationToken);
    }
}

public sealed record UpdateSpecializationCommand(
    long SpecializationId,
    string Name,
    string RowVersion) : ICommand<SpecializationModel>;

public sealed class UpdateSpecializationCommandHandler
    : ICommandHandler<UpdateSpecializationCommand, SpecializationModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISpecializationCatalogService _service;

    public UpdateSpecializationCommandHandler(
        ICurrentUser currentUser,
        ISpecializationCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<SpecializationModel>> Handle(
        UpdateSpecializationCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CatalogCommandValidation.GetActor(_currentUser);
        Result<byte[]> version = CatalogCommandValidation.DecodeRowVersion(command.RowVersion);
        Result name = CatalogCommandValidation.ValidateName(command.Name, "اسم التخصص", 150);
        if (actor.IsFailure || version.IsFailure || name.IsFailure)
        {
            ResultError error = actor.IsFailure
                ? actor.Error
                : version.IsFailure ? version.Error : name.Error;
            return Task.FromResult(Result.Failure<SpecializationModel>(error));
        }

        return _service.UpdateAsync(
            actor.Value,
            command.SpecializationId,
            command.Name.Trim(),
            version.Value,
            cancellationToken);
    }
}

public sealed record ArchiveSpecializationCommand(
    long SpecializationId,
    string RowVersion) : ICommand;

public sealed class ArchiveSpecializationCommandHandler
    : ICommandHandler<ArchiveSpecializationCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISpecializationCatalogService _service;

    public ArchiveSpecializationCommandHandler(
        ICurrentUser currentUser,
        ISpecializationCatalogService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(
        ArchiveSpecializationCommand command,
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
            command.SpecializationId,
            version.Value,
            cancellationToken);
    }
}
