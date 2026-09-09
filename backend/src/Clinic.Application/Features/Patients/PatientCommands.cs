using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Abstractions.Patients;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using Clinic.Domain.Patients;

namespace Clinic.Application.Features.Patients;

public sealed record CreatePatientCommand(PatientInput Input) : ICommand<PatientDetails>;

public sealed class CreatePatientCommandHandler : ICommandHandler<CreatePatientCommand, PatientDetails>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientAdministrationService _service;

    public CreatePatientCommandHandler(ICurrentUser currentUser, IPatientAdministrationService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientDetails>> Handle(CreatePatientCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result<PatientInput> input = PatientValidation.Input(command.Input);
        if (actor.IsFailure || input.IsFailure)
        {
            return Task.FromResult(Result.Failure<PatientDetails>(
                actor.IsFailure ? actor.Error : input.Error));
        }

        return _service.CreatePatientAsync(actor.Value, input.Value, cancellationToken);
    }
}

public sealed record UpdatePatientCommand(long PatientId, PatientInput Input, string RowVersion)
    : ICommand<PatientDetails>;

public sealed class UpdatePatientCommandHandler : ICommandHandler<UpdatePatientCommand, PatientDetails>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientAdministrationService _service;

    public UpdatePatientCommandHandler(ICurrentUser currentUser, IPatientAdministrationService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<PatientDetails>> Handle(UpdatePatientCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result<PatientInput> input = PatientValidation.Input(command.Input);
        Result<byte[]> version = PatientValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || input.IsFailure || version.IsFailure)
        {
            ResultError error = actor.IsFailure ? actor.Error : input.IsFailure
                ? input.Error : version.Error;
            return Task.FromResult(Result.Failure<PatientDetails>(error));
        }

        return _service.UpdatePatientAsync(actor.Value, command.PatientId, input.Value,
            version.Value, cancellationToken);
    }
}

public sealed record ArchivePatientCommand(long PatientId, string Reason, string RowVersion)
    : ICommand;

public sealed class ArchivePatientCommandHandler : ICommandHandler<ArchivePatientCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientAdministrationService _service;

    public ArchivePatientCommandHandler(ICurrentUser currentUser, IPatientAdministrationService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(ArchivePatientCommand command, CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result reason = PatientValidation.RequiredText(command.Reason, "سبب الأرشفة", 500);
        Result<byte[]> version = PatientValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error :
                reason.IsFailure ? reason.Error : version.Error));
        }

        return _service.ArchivePatientAsync(actor.Value, command.PatientId,
            command.Reason.Trim(), version.Value, cancellationToken);
    }
}

public sealed record CreatePatientNoteCommand(long PatientId, string NoteText,
    PatientNoteVisibility Visibility) : ICommand<SensitiveNoteReceipt>;

public sealed class CreatePatientNoteCommandHandler
    : ICommandHandler<CreatePatientNoteCommand, SensitiveNoteReceipt>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientAdministrationService _service;

    public CreatePatientNoteCommandHandler(ICurrentUser currentUser,
        IPatientAdministrationService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<SensitiveNoteReceipt>> Handle(CreatePatientNoteCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result text = PatientValidation.RequiredText(command.NoteText, "الملاحظة", 2000);
        if (actor.IsFailure || text.IsFailure || !Enum.IsDefined(command.Visibility))
        {
            ResultError error = actor.IsFailure ? actor.Error : text.IsFailure
                ? text.Error : PatientErrors.Validation("درجة ظهور الملاحظة غير صحيحة.");
            return Task.FromResult(Result.Failure<SensitiveNoteReceipt>(error));
        }

        return _service.CreateNoteAsync(actor.Value, command.PatientId,
            command.NoteText.Trim(), command.Visibility, cancellationToken);
    }
}

public sealed record ArchivePatientNoteCommand(long NoteId, string Reason, string RowVersion)
    : ICommand;

public sealed class ArchivePatientNoteCommandHandler : ICommandHandler<ArchivePatientNoteCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientAdministrationService _service;

    public ArchivePatientNoteCommandHandler(ICurrentUser currentUser,
        IPatientAdministrationService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(ArchivePatientNoteCommand command,
        CancellationToken cancellationToken) =>
        Archive(actor: PatientValidation.Actor(_currentUser),
            reason: PatientValidation.RequiredText(command.Reason, "سبب الأرشفة", 500),
            version: PatientValidation.RowVersion(command.RowVersion), command,
            cancellationToken);

    private Task<Result> Archive(Result<long> actor, Result reason, Result<byte[]> version,
        ArchivePatientNoteCommand command, CancellationToken cancellationToken)
    {
        if (actor.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error :
                reason.IsFailure ? reason.Error : version.Error));
        }

        return _service.ArchiveNoteAsync(actor.Value, command.NoteId, command.Reason.Trim(),
            version.Value, cancellationToken);
    }
}

public sealed record CreateTreatmentHistoryCommand(long PatientId, DateOnly EventDate,
    string Description) : ICommand<TreatmentHistoryModel>;

public sealed class CreateTreatmentHistoryCommandHandler
    : ICommandHandler<CreateTreatmentHistoryCommand, TreatmentHistoryModel>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientAdministrationService _service;

    public CreateTreatmentHistoryCommandHandler(ICurrentUser currentUser,
        IPatientAdministrationService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result<TreatmentHistoryModel>> Handle(CreateTreatmentHistoryCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result description = PatientValidation.RequiredText(command.Description,
            "وصف التاريخ العلاجي", 2000);
        if (actor.IsFailure || description.IsFailure)
        {
            return Task.FromResult(Result.Failure<TreatmentHistoryModel>(
                actor.IsFailure ? actor.Error : description.Error));
        }

        return _service.CreateTreatmentHistoryAsync(actor.Value, command.PatientId,
            command.EventDate, command.Description.Trim(), cancellationToken);
    }
}

public sealed record ArchiveTreatmentHistoryCommand(long TreatmentHistoryId, string Reason,
    string RowVersion) : ICommand;

public sealed class ArchiveTreatmentHistoryCommandHandler
    : ICommandHandler<ArchiveTreatmentHistoryCommand>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPatientAdministrationService _service;

    public ArchiveTreatmentHistoryCommandHandler(ICurrentUser currentUser,
        IPatientAdministrationService service)
    {
        _currentUser = currentUser;
        _service = service;
    }

    public Task<Result> Handle(ArchiveTreatmentHistoryCommand command,
        CancellationToken cancellationToken)
    {
        Result<long> actor = PatientValidation.Actor(_currentUser);
        Result reason = PatientValidation.RequiredText(command.Reason, "سبب الأرشفة", 500);
        Result<byte[]> version = PatientValidation.RowVersion(command.RowVersion);
        if (actor.IsFailure || reason.IsFailure || version.IsFailure)
        {
            return Task.FromResult(Result.Failure(actor.IsFailure ? actor.Error :
                reason.IsFailure ? reason.Error : version.Error));
        }

        return _service.ArchiveTreatmentHistoryAsync(actor.Value, command.TreatmentHistoryId,
            command.Reason.Trim(), version.Value, cancellationToken);
    }
}
