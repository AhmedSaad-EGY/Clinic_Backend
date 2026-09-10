using Clinic.Application.Abstractions.Discounts;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using Clinic.Domain.Discounts;

namespace Clinic.Application.Features.Discounts;

public sealed record CreateDiscountCommand(DiscountDefinition Definition)
    : ICommand<DiscountModel>;

public sealed class CreateDiscountCommandHandler(ICurrentUser currentUser,
    IDiscountService service) : ICommandHandler<CreateDiscountCommand, DiscountModel>
{
    public Task<Result<DiscountModel>> Handle(CreateDiscountCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = Actor(currentUser);
        Result validation = Validate(command.Definition);
        return actor.IsFailure || validation.IsFailure
            ? Task.FromResult(Result.Failure<DiscountModel>(
                actor.IsFailure ? actor.Error : validation.Error))
            : service.CreateAsync(actor.Value, command.Definition, cancellationToken);
    }

    internal static Result<long> Actor(ICurrentUser user) => user.UserId is long id && id > 0
        ? Result.Success(id) : Result.Failure<long>(DiscountErrors.NotAuthenticated);

    internal static Result Validate(DiscountDefinition? definition)
    {
        if (definition is null || string.IsNullOrWhiteSpace(definition.Name) ||
            definition.Name.Trim().Length > 200 || !Enum.IsDefined(definition.Type) ||
            !Enum.IsDefined(definition.AppliesTo) || !Enum.IsDefined(definition.ScopeMode) ||
            definition.EndAt <= definition.StartAt || definition.Value < 0 ||
            definition.Value > Discount.MaximumMoney ||
            decimal.Round(definition.Value, 2) != definition.Value ||
            definition.Type == DiscountType.Percentage && definition.Value > 100 ||
            definition.Type == DiscountType.FixedAmount && definition.Value <= 0)
        {
            return Result.Failure(DiscountErrors.Validation(
                "بيانات الخصم أو فترة سريانه غير صحيحة."));
        }

        DiscountTargetInput targets = definition.Targets;
        if (targets is null || targets.DepartmentIds.Any(id => id <= 0) ||
            targets.ServiceIds.Any(id => id <= 0) || targets.PackageIds.Any(id => id <= 0) ||
            targets.DepartmentIds.Distinct().Count() != targets.DepartmentIds.Count ||
            targets.ServiceIds.Distinct().Count() != targets.ServiceIds.Count ||
            targets.PackageIds.Distinct().Count() != targets.PackageIds.Count)
        {
            return Result.Failure(DiscountErrors.Validation("أهداف الخصم غير صحيحة أو مكررة."));
        }

        return Result.Success();
    }
}

public sealed record UpdateDiscountCommand(long DiscountId, DiscountDefinition Definition,
    string RowVersion) : ICommand<DiscountModel>;

public sealed class UpdateDiscountCommandHandler(ICurrentUser currentUser,
    IDiscountService service) : ICommandHandler<UpdateDiscountCommand, DiscountModel>
{
    public Task<Result<DiscountModel>> Handle(UpdateDiscountCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CreateDiscountCommandHandler.Actor(currentUser);
        Result validation = CreateDiscountCommandHandler.Validate(command.Definition);
        Result<byte[]> version = DecodeVersion(command.RowVersion);
        if (actor.IsFailure || validation.IsFailure || version.IsFailure || command.DiscountId <= 0)
        {
            ResultError error = actor.IsFailure ? actor.Error : validation.IsFailure
                ? validation.Error : version.IsFailure ? version.Error
                : DiscountErrors.Validation("رقم الخصم غير صحيح.");
            return Task.FromResult(Result.Failure<DiscountModel>(error));
        }

        return service.UpdateAsync(actor.Value, command.DiscountId, command.Definition,
            version.Value, cancellationToken);
    }

    internal static Result<byte[]> DecodeVersion(string? value)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(value ?? string.Empty);
            return bytes.Length == 8 ? Result.Success(bytes) : Result.Failure<byte[]>(
                DiscountErrors.Validation("نسخة السجل غير صحيحة."));
        }
        catch (FormatException)
        {
            return Result.Failure<byte[]>(DiscountErrors.Validation("نسخة السجل غير صحيحة."));
        }
    }
}

public sealed record SetDiscountActivationCommand(long DiscountId, bool IsActive,
    string RowVersion) : ICommand<DiscountModel>;

public sealed class SetDiscountActivationCommandHandler(ICurrentUser currentUser,
    IDiscountService service) : ICommandHandler<SetDiscountActivationCommand, DiscountModel>
{
    public Task<Result<DiscountModel>> Handle(SetDiscountActivationCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CreateDiscountCommandHandler.Actor(currentUser);
        Result<byte[]> version = UpdateDiscountCommandHandler.DecodeVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.DiscountId <= 0)
        {
            return Task.FromResult(Result.Failure<DiscountModel>(actor.IsFailure
                ? actor.Error : version.IsFailure ? version.Error
                : DiscountErrors.Validation("رقم الخصم غير صحيح.")));
        }
        return service.SetActiveAsync(actor.Value, command.DiscountId, command.IsActive,
            version.Value, cancellationToken);
    }
}

public sealed record ArchiveDiscountCommand(long DiscountId, string RowVersion) : ICommand;

public sealed class ArchiveDiscountCommandHandler(ICurrentUser currentUser,
    IDiscountService service) : ICommandHandler<ArchiveDiscountCommand>
{
    public async Task<Result> Handle(ArchiveDiscountCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CreateDiscountCommandHandler.Actor(currentUser);
        Result<byte[]> version = UpdateDiscountCommandHandler.DecodeVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.DiscountId <= 0)
        {
            return Result.Failure(actor.IsFailure ? actor.Error : version.IsFailure
                ? version.Error : DiscountErrors.Validation("رقم الخصم غير صحيح."));
        }
        return await service.ArchiveAsync(actor.Value, command.DiscountId,
            version.Value, cancellationToken);
    }
}
