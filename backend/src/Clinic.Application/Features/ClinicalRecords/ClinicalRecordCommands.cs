using Clinic.Application.Abstractions.ClinicalRecords;
using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;

namespace Clinic.Application.Features.ClinicalRecords;

public sealed record CreatePrescriptionDraftCommand(long AppointmentServiceId,
    PrescriptionContentInput Content) : ICommand<PrescriptionModel>;
public sealed record SavePrescriptionDraftCommand(long PrescriptionId,
    PrescriptionContentInput Content, string RowVersion) : ICommand<PrescriptionModel>;
public sealed record FinalizePrescriptionCommand(long PrescriptionId,
    bool MatchesDoctorPrescription, string RowVersion) : ICommand<PrescriptionModel>;
public sealed record CorrectPrescriptionCommand(long PrescriptionId,
    PrescriptionContentInput Content, string Reason,
    bool MatchesDoctorPrescription, string RowVersion) : ICommand<PrescriptionModel>;
public sealed record VoidPrescriptionCommand(long PrescriptionId, string Reason,
    string RowVersion) : ICommand<PrescriptionModel>;
public sealed record CancelFollowUpCommand(long FollowUpId, string Reason,
    string RowVersion) : ICommand<FollowUpModel>;

public sealed class CreatePrescriptionDraftCommandHandler(ICurrentUser currentUser,
    IPrescriptionCommandService service)
    : ICommandHandler<CreatePrescriptionDraftCommand, PrescriptionModel>
{
    public Task<Result<PrescriptionModel>> Handle(CreatePrescriptionDraftCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        Result content = ClinicalRecordValidation.Content(command.Content);
        return actor.IsFailure || content.IsFailure
            ? Task.FromResult(Result.Failure<PrescriptionModel>(
                actor.IsFailure ? actor.Error : content.Error))
            : service.CreateDraftAsync(actor.Value, command.AppointmentServiceId,
                command.Content, cancellationToken);
    }
}

public sealed class SavePrescriptionDraftCommandHandler(ICurrentUser currentUser,
    IPrescriptionCommandService service)
    : ICommandHandler<SavePrescriptionDraftCommand, PrescriptionModel>
{
    public Task<Result<PrescriptionModel>> Handle(SavePrescriptionDraftCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        Result content = ClinicalRecordValidation.Content(command.Content);
        Result<byte[]> version = ClinicalRecordValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || content.IsFailure || version.IsFailure)
            return Task.FromResult(Result.Failure<PrescriptionModel>(actor.IsFailure
                ? actor.Error : content.IsFailure ? content.Error : version.Error));
        return service.SaveDraftAsync(actor.Value, command.PrescriptionId,
            command.Content, version.Value, cancellationToken);
    }
}

public sealed class FinalizePrescriptionCommandHandler(ICurrentUser currentUser,
    IPrescriptionCommandService service)
    : ICommandHandler<FinalizePrescriptionCommand, PrescriptionModel>
{
    public Task<Result<PrescriptionModel>> Handle(FinalizePrescriptionCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        Result<byte[]> version = ClinicalRecordValidation.RowVersion(command.RowVersion);
        return actor.IsFailure || version.IsFailure || !command.MatchesDoctorPrescription
            ? Task.FromResult(Result.Failure<PrescriptionModel>(
                actor.IsFailure ? actor.Error : version.IsFailure ? version.Error
                    : ClinicalRecordErrors.Validation(
                        "يجب تأكيد مطابقة الروشتة لروشتة الطبيب.")))
            : service.FinalizeAsync(actor.Value, command.PrescriptionId,
                command.MatchesDoctorPrescription, version.Value, cancellationToken);
    }
}

public sealed class CorrectPrescriptionCommandHandler(ICurrentUser currentUser,
    IPrescriptionCommandService service)
    : ICommandHandler<CorrectPrescriptionCommand, PrescriptionModel>
{
    public Task<Result<PrescriptionModel>> Handle(CorrectPrescriptionCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        Result content = ClinicalRecordValidation.Content(command.Content);
        Result reason = ClinicalRecordValidation.RequiredReason(command.Reason);
        Result<byte[]> version = ClinicalRecordValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || content.IsFailure || reason.IsFailure || version.IsFailure ||
            !command.MatchesDoctorPrescription)
            return Task.FromResult(Result.Failure<PrescriptionModel>(actor.IsFailure
                ? actor.Error : content.IsFailure ? content.Error : reason.IsFailure
                    ? reason.Error : version.IsFailure ? version.Error
                    : ClinicalRecordErrors.Validation(
                        "يجب تأكيد مطابقة التصحيح لروشتة الطبيب.")));
        return service.CorrectAsync(actor.Value, command.PrescriptionId,
            command.Content, command.Reason.Trim(), true, version.Value, cancellationToken);
    }
}

public sealed class VoidPrescriptionCommandHandler(ICurrentUser currentUser,
    IPrescriptionCommandService service)
    : ICommandHandler<VoidPrescriptionCommand, PrescriptionModel>
{
    public Task<Result<PrescriptionModel>> Handle(VoidPrescriptionCommand command,
        CancellationToken cancellationToken) => ClinicalCommandSupport.WithReason(currentUser, command.Reason,
            command.RowVersion, (actor, version) => service.VoidAsync(actor,
                command.PrescriptionId, command.Reason.Trim(), version, cancellationToken));
}

public sealed class CancelFollowUpCommandHandler(ICurrentUser currentUser,
    IFollowUpCommandService service)
    : ICommandHandler<CancelFollowUpCommand, FollowUpModel>
{
    public Task<Result<FollowUpModel>> Handle(CancelFollowUpCommand command,
        CancellationToken cancellationToken) => ClinicalCommandSupport.WithReason(currentUser, command.Reason,
            command.RowVersion, (actor, version) => service.CancelFollowUpAsync(actor,
                command.FollowUpId, command.Reason.Trim(), version, cancellationToken));
}

file static class ClinicalCommandSupport
{
    public static Task<Result<T>> WithReason<T>(ICurrentUser currentUser,
        string reason, string rowVersion,
        Func<long, byte[], Task<Result<T>>> action)
    {
        Result<long> actor = ClinicalRecordValidation.Actor(currentUser);
        Result validReason = ClinicalRecordValidation.RequiredReason(reason);
        Result<byte[]> version = ClinicalRecordValidation.RowVersion(rowVersion);
        return actor.IsFailure || validReason.IsFailure || version.IsFailure
            ? Task.FromResult(Result.Failure<T>(actor.IsFailure ? actor.Error
                : validReason.IsFailure ? validReason.Error : version.Error))
            : action(actor.Value, version.Value);
    }
}
