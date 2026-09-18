namespace Clinic.Domain.Cashier;

public sealed class CashDrawer : AggregateRoot
{
    private CashDrawer()
    {
    }

    private CashDrawer(long secretaryUserId, string name, DateTimeOffset createdAt)
    {
        CashierGuard.PositiveId(secretaryUserId, "السكرتيرة");
        SecretaryUserId = secretaryUserId;
        Name = CashierGuard.RequiredText(name, "اسم الدرج", 220);
        IsActive = true;
        CreatedAt = createdAt;
    }

    public long SecretaryUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static CashDrawer Create(long secretaryUserId, string secretaryName,
        DateTimeOffset createdAt) => new(secretaryUserId, $"درج - {secretaryName.Trim()}", createdAt);
}
