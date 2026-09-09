using Clinic.Domain.Common;

namespace Clinic.Domain.Cashier;

public sealed class PaymentMethod : Entity
{
    private PaymentMethod()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsCash { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }
}
