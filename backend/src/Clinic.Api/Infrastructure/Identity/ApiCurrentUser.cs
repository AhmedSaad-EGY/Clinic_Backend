using System.Globalization;
using System.Security.Claims;
using Clinic.Application.Abstractions.Identity;

namespace Clinic.Api.Infrastructure.Identity;

public sealed class ApiCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? UserId
    {
        get
        {
            string? value = _httpContextAccessor.HttpContext?.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long userId)
                ? userId
                : null;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
