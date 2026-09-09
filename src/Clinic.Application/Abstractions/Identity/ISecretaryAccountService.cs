using Clinic.Application.Common;

namespace Clinic.Application.Abstractions.Identity;

public interface ISecretaryAccountService
{
    Task<Result<SecretarySummary>> CreateAsync(
        long adminUserId,
        string fullName,
        string phoneNumber,
        string? email,
        string userName,
        string temporaryPassword,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<SecretarySummary>>> ListAsync(
        long adminUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result> DisableAsync(
        long adminUserId,
        long secretaryUserId,
        string reason,
        CancellationToken cancellationToken);

    Task<Result> EnableAsync(
        long adminUserId,
        long secretaryUserId,
        CancellationToken cancellationToken);

    Task<Result> UnlockAsync(
        long adminUserId,
        long secretaryUserId,
        CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(
        long adminUserId,
        long secretaryUserId,
        string temporaryPassword,
        CancellationToken cancellationToken);
}
