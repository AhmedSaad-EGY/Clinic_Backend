namespace Clinic.Application.Abstractions.Identity;

public interface IAuthenticationService
{
    Task<Result<UserProfile>> SignInAsync(
        string userName,
        string password,
        CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);

    Task<Result<UserProfile>> GetUserAsync(
        long userId,
        CancellationToken cancellationToken);

    Task<Result<UserProfile>> ChangePasswordAsync(
        long userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);
}
