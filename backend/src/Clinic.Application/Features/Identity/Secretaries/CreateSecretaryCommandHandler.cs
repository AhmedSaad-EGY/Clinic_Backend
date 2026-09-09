using Clinic.Application.Abstractions.Identity;
using Clinic.Application.Common;
using Clinic.Application.Messaging;
using System.Net.Mail;

namespace Clinic.Application.Features.Identity.Secretaries;

public sealed class CreateSecretaryCommandHandler
    : ICommandHandler<CreateSecretaryCommand, SecretarySummary>
{
    private readonly ICurrentUser _currentUser;
    private readonly ISecretaryAccountService _secretaryAccountService;

    public CreateSecretaryCommandHandler(
        ICurrentUser currentUser,
        ISecretaryAccountService secretaryAccountService)
    {
        _currentUser = currentUser;
        _secretaryAccountService = secretaryAccountService;
    }

    public Task<Result<SecretarySummary>> Handle(
        CreateSecretaryCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not long adminUserId)
        {
            return Task.FromResult(Result.Failure<SecretarySummary>(
                IdentityErrors.NotAuthenticated));
        }

        if (string.IsNullOrWhiteSpace(command.FullName) ||
            string.IsNullOrWhiteSpace(command.PhoneNumber) ||
            string.IsNullOrWhiteSpace(command.UserName) ||
            string.IsNullOrWhiteSpace(command.TemporaryPassword))
        {
            return Task.FromResult(Result.Failure<SecretarySummary>(
                IdentityErrors.Validation(
                    "الاسم ورقم الهاتف واسم المستخدم وكلمة المرور المؤقتة مطلوبة.")));
        }

        if (command.FullName.Trim().Length > 200 ||
            command.UserName.Trim().Length > 256 ||
            command.TemporaryPassword.Length > 128)
        {
            return Task.FromResult(Result.Failure<SecretarySummary>(
                IdentityErrors.Validation("أحد الحقول يتجاوز الطول المسموح.")));
        }

        string? email = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();
        if (email is not null && (email.Length > 256 || !MailAddress.TryCreate(email, out _)))
        {
            return Task.FromResult(Result.Failure<SecretarySummary>(
                IdentityErrors.Validation("البريد الإلكتروني غير صحيح.")));
        }

        return _secretaryAccountService.CreateAsync(
            adminUserId,
            command.FullName.Trim(),
            command.PhoneNumber.Trim(),
            email,
            command.UserName.Trim(),
            command.TemporaryPassword,
            cancellationToken);
    }
}
