namespace Clinic.Domain.Cashier;

public sealed class ShiftPolicy : AggregateRoot
{
    public const long SingletonId = 1;
    public const int DefaultClosingGraceMinutes = 10;
    public const int MaximumClosingGraceMinutes = 120;

    private ShiftPolicy()
    {
    }

    public int ClosingGraceMinutes { get; private set; }

    public long? UpdatedByAdminUserId { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static ShiftPolicy CreateDefault() => new()
    {
        Id = SingletonId,
        ClosingGraceMinutes = DefaultClosingGraceMinutes
    };

    public void Update(int closingGraceMinutes, long adminUserId,
        DateTimeOffset updatedAt)
    {
        CashierGuard.GraceMinutes(closingGraceMinutes);
        CashierGuard.PositiveId(adminUserId, "الأدمن");
        ClosingGraceMinutes = closingGraceMinutes;
        UpdatedByAdminUserId = adminUserId;
        UpdatedAt = updatedAt;
    }
}
