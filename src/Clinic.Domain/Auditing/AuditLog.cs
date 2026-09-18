namespace Clinic.Domain.Auditing;

public sealed class AuditLog : Entity
{
    private AuditLog()
    {
    }

    private AuditLog(
        long? actorUserId,
        AuditActorType actorType,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset occurredAt,
        string? reason,
        string? dataJson)
    {
        ActorUserId = actorUserId;
        ActorType = actorType;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OccurredAt = occurredAt;
        Reason = reason;
        DataJson = dataJson;
    }

    public long? ActorUserId { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public string? Reason { get; private set; }

    public string? DataJson { get; private set; }

    public static AuditLog CreateForUser(
        long actorUserId,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset occurredAt,
        string? reason = null,
        string? dataJson = null)
    {
        if (actorUserId <= 0)
        {
            throw new DomainException("The audit actor user identifier is invalid.");
        }

        return Create(
            actorUserId,
            AuditActorType.User,
            action,
            entityType,
            entityId,
            occurredAt,
            reason,
            dataJson);
    }

    public static AuditLog CreateForSystem(
        string action,
        string entityType,
        string entityId,
        DateTimeOffset occurredAt,
        string? reason = null,
        string? dataJson = null) =>
        Create(
            actorUserId: null,
            AuditActorType.System,
            action,
            entityType,
            entityId,
            occurredAt,
            reason,
            dataJson);

    private static AuditLog Create(
        long? actorUserId,
        AuditActorType actorType,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset occurredAt,
        string? reason,
        string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainException("The audit action is required.");
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainException("The audited entity type is required.");
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new DomainException("The audited entity identifier is required.");
        }

        return new AuditLog(
            actorUserId,
            actorType,
            action.Trim(),
            entityType.Trim(),
            entityId.Trim(),
            occurredAt,
            NormalizeOptional(reason),
            NormalizeOptional(dataJson));
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
