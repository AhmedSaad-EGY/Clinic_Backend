namespace Clinic.Application.Features.Packages;

public sealed record CreatePackageCommand(long DepartmentId, string Name, decimal BasePrice,
    int ActivationGraceDays, int UsageDurationDays,
    IReadOnlyCollection<PackageServiceInput> Services) : ICommand<PackageModel>;

public sealed class CreatePackageCommandHandler : ICommandHandler<CreatePackageCommand, PackageModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPackageCommandService _service;

    public CreatePackageCommandHandler(ICurrentUser currentUser, IPackageCommandService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PackageModel>> Handle(CreatePackageCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = GetActor(_currentUser);
        Result validation = Validate(command.DepartmentId, command.Name, command.BasePrice,
            command.ActivationGraceDays, command.UsageDurationDays, command.Services);
        if (actor.IsFailure || validation.IsFailure)
        {
            return Task.FromResult(Result.Failure<PackageModel>(
                actor.IsFailure ? actor.Error : validation.Error));
        }

        return _service.CreateAsync(actor.Value, command.DepartmentId, command.Name.Trim(),
            command.BasePrice, command.ActivationGraceDays, command.UsageDurationDays,
            command.Services, cancellationToken);
    }

    internal static Result Validate(long departmentId, string? name, decimal basePrice,
        int activationGraceDays, int usageDurationDays,
        IReadOnlyCollection<PackageServiceInput>? services)
    {
        if (departmentId <= 0 || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            return Result.Failure(PackageErrors.Validation("القسم واسم الباقة مطلوبان."));
        }

        if (basePrice < 0 || basePrice > Package.MaximumMoney ||
            decimal.Round(basePrice, 2) != basePrice)
        {
            return Result.Failure(PackageErrors.Validation(
                "سعر الباقة لا يمكن أن يكون سالبًا ويقبل منزلتين عشريتين بحد أقصى."));
        }

        if (activationGraceDays <= 0 || usageDurationDays <= 0)
        {
            return Result.Failure(PackageErrors.Validation(
                "مهلة بدء الاستخدام ومدة الاستخدام يجب أن تكونا عدد أيام موجبًا."));
        }

        if (services is null || services.Count is < 1 or > Package.MaximumServiceCount ||
            services.Any(item => item.ServiceId <= 0 || item.SessionsIncluded <= 0 ||
                                 item.UnitPriceAtDefinition <= 0 ||
                                 item.UnitPriceAtDefinition > Package.MaximumMoney ||
                                 decimal.Round(item.UnitPriceAtDefinition, 2) !=
                                 item.UnitPriceAtDefinition) ||
            services.Select(item => item.ServiceId).Distinct().Count() != services.Count ||
            services.Sum(item => (long)item.SessionsIncluded) > Package.MaximumSessionCount)
        {
            return Result.Failure(PackageErrors.Validation(
                "خدمات الباقة غير صحيحة أو مكررة، أو تتجاوز الحدود المسموحة."));
        }

        return Result.Success();
    }

    internal static Result<long> GetActor(ICurrentUser currentUser) =>
        currentUser.UserId is long id && id > 0
            ? Result.Success(id)
            : Result.Failure<long>(PackageErrors.NotAuthenticated);
}

public sealed record UpdatePackageCommand(long PackageId, string Name, decimal BasePrice,
    int ActivationGraceDays, int UsageDurationDays,
    IReadOnlyCollection<PackageServiceInput> Services, string RowVersion) : ICommand<PackageModel>;

public sealed class UpdatePackageCommandHandler : ICommandHandler<UpdatePackageCommand, PackageModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPackageCommandService _service;

    public UpdatePackageCommandHandler(ICurrentUser currentUser, IPackageCommandService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PackageModel>> Handle(UpdatePackageCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CreatePackageCommandHandler.GetActor(_currentUser);
        Result validation = CreatePackageCommandHandler.Validate(1, command.Name,
            command.BasePrice, command.ActivationGraceDays, command.UsageDurationDays,
            command.Services);
        Result<byte[]> version = DecodeVersion(command.RowVersion);
        if (actor.IsFailure || validation.IsFailure || version.IsFailure || command.PackageId <= 0)
        {
            ResultError error = actor.IsFailure ? actor.Error : validation.IsFailure
                ? validation.Error : version.IsFailure ? version.Error
                : PackageErrors.Validation("رقم الباقة غير صحيح.");
            return Task.FromResult(Result.Failure<PackageModel>(error));
        }

        return _service.UpdateAsync(actor.Value, command.PackageId, command.Name.Trim(),
            command.BasePrice, command.ActivationGraceDays, command.UsageDurationDays,
            command.Services, version.Value, cancellationToken);
    }

    internal static Result<byte[]> DecodeVersion(string? value)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(value ?? string.Empty);
            return bytes.Length == 8 ? Result.Success(bytes) : Result.Failure<byte[]>(
                PackageErrors.Validation("نسخة السجل غير صحيحة."));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(PackageErrors.Validation("نسخة السجل غير صحيحة."));
        }
    }
}

public sealed record SetPackageActivationCommand(long PackageId, bool IsActive,
    string RowVersion) : ICommand<PackageModel>;

public sealed class SetPackageActivationCommandHandler
    : ICommandHandler<SetPackageActivationCommand, PackageModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPackageCommandService _service;

    public SetPackageActivationCommandHandler(ICurrentUser currentUser,
        IPackageCommandService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PackageModel>> Handle(SetPackageActivationCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CreatePackageCommandHandler.GetActor(_currentUser);
        Result<byte[]> version = UpdatePackageCommandHandler.DecodeVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.PackageId <= 0)
        {
            return Task.FromResult(Result.Failure<PackageModel>(actor.IsFailure
                ? actor.Error : version.IsFailure ? version.Error
                : PackageErrors.Validation("رقم الباقة غير صحيح.")));
        }

        return _service.SetActiveAsync(actor.Value, command.PackageId, command.IsActive,
            version.Value, cancellationToken);
    }
}

public sealed record ArchivePackageCommand(long PackageId, string RowVersion) : ICommand;

public sealed class ArchivePackageCommandHandler : ICommandHandler<ArchivePackageCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPackageCommandService _service;

    public ArchivePackageCommandHandler(ICurrentUser currentUser, IPackageCommandService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(ArchivePackageCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = CreatePackageCommandHandler.GetActor(_currentUser);
        Result<byte[]> version = UpdatePackageCommandHandler.DecodeVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.PackageId <= 0)
        {
            return Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error
                : version.IsFailure ? version.Error
                : PackageErrors.Validation("رقم الباقة غير صحيح.")));
        }

        return _service.ArchiveAsync(actor.Value, command.PackageId, version.Value,
            cancellationToken);
    }
}
