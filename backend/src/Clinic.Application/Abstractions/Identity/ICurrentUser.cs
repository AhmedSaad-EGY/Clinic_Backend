namespace Clinic.Application.Abstractions.Identity;

public interface ICurrentUser
{
    long? UserId { get; }

    bool IsAuthenticated { get; }
}
