using Clinic.Domain.Common;

namespace Clinic.Domain.Catalog;

public sealed class ServicePriceHistory : Entity
{
    private ServicePriceHistory()
    {
    }

    internal ServicePriceHistory(
        Service service,
        decimal unitPrice,
        DateTimeOffset effectiveFrom,
        long changedByUserId)
    {
        ArgumentNullException.ThrowIfNull(service);
        CatalogGuard.PositiveMoney(unitPrice);
        CatalogGuard.PositiveIdentifier(changedByUserId, "المستخدم");
        Service = service;
        ServiceId = service.Id;
        UnitPrice = unitPrice;
        EffectiveFrom = effectiveFrom;
        ChangedByUserId = changedByUserId;
        ChangedAt = effectiveFrom;
    }

    public long ServiceId { get; private set; }

    public Service Service { get; private set; } = null!;

    public decimal UnitPrice { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public long ChangedByUserId { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    internal void Close(DateTimeOffset effectiveTo)
    {
        if (EffectiveTo is not null || effectiveTo <= EffectiveFrom)
        {
            throw new DomainException("فترة السعر غير صحيحة.");
        }

        EffectiveTo = effectiveTo;
    }
}
