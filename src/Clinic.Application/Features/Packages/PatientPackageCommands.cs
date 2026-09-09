using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Packages;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using Clinic.Domain.Packages;

namespace Clinic.Application.Features.Packages;

public sealed record RegisterPatientPackageCommand(long PatientId, long PackageId,
    string PackageRowVersion, Guid IdempotencyKey) : ICommand<PatientPackageRegistrationResult>;

public sealed class RegisterPatientPackageCommandHandler
    : ICommandHandler<RegisterPatientPackageCommand, PatientPackageRegistrationResult>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientPackageCommandService _service;

    public RegisterPatientPackageCommandHandler(ICurrentUser currentUser,
        IPatientPackageCommandService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientPackageRegistrationResult>> Handle(
        RegisterPatientPackageCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = CreatePackageCommandHandler.GetActor(_currentUser);
        Result<byte[]> version = UpdatePackageCommandHandler.DecodeVersion(
            command.PackageRowVersion);
        if (actor.IsFailure || version.IsFailure || command.PatientId <= 0 ||
            command.PackageId <= 0 || command.IdempotencyKey == Guid.Empty)
        {
            ResultError error = actor.IsFailure ? actor.Error : version.IsFailure
                ? version.Error : PackageErrors.Validation("بيانات تسجيل الباقة غير صحيحة.");
            return Task.FromResult(Result.Failure<PatientPackageRegistrationResult>(error));
        }

        return _service.RegisterAsync(actor.Value, command.PatientId, command.PackageId,
            version.Value, command.IdempotencyKey, cancellationToken);
    }
}

public sealed record ExtendPatientPackageCommand(long PatientPackageId,
    PatientPackageExtensionType ExtensionType, DateTimeOffset NewDeadline, string Reason,
    string RowVersion) : ICommand<PatientPackageModel>;

public sealed class ExtendPatientPackageCommandHandler
    : ICommandHandler<ExtendPatientPackageCommand, PatientPackageModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientPackageCommandService _service;

    public ExtendPatientPackageCommandHandler(ICurrentUser currentUser,
        IPatientPackageCommandService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientPackageModel>> Handle(ExtendPatientPackageCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = CreatePackageCommandHandler.GetActor(_currentUser);
        Result<byte[]> version = UpdatePackageCommandHandler.DecodeVersion(command.RowVersion);
        if (actor.IsFailure || version.IsFailure || command.PatientPackageId <= 0 ||
            !Enum.IsDefined(command.ExtensionType) || string.IsNullOrWhiteSpace(command.Reason) ||
            command.Reason.Trim().Length > 500)
        {
            ResultError error = actor.IsFailure ? actor.Error : version.IsFailure
                ? version.Error : PackageErrors.Validation(
                    "نوع التمديد والموعد والسبب مطلوبة، والسبب لا يتجاوز 500 حرف.");
            return Task.FromResult(Result.Failure<PatientPackageModel>(error));
        }

        return _service.ExtendAsync(actor.Value, command.PatientPackageId,
            command.ExtensionType, command.NewDeadline.ToUniversalTime(), command.Reason.Trim(),
            version.Value, cancellationToken);
    }
}
