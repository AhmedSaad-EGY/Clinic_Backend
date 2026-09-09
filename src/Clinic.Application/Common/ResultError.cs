namespace Clinic.Application.Common;

public sealed record ResultError(
    string Code,
    string Description,
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static readonly ResultError None = new(string.Empty, string.Empty);
}
