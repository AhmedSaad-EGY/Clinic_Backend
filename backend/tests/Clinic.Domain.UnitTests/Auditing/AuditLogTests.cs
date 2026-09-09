using Clinic.Domain.Auditing;
using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Auditing;

public sealed class AuditLogTests
{
    [Fact]
    public void CreateForUserKeepsActorAndNormalizesOptionalValues()
    {
        DateTimeOffset occurredAt = new(2026, 9, 5, 12, 30, 0, TimeSpan.Zero);

        AuditLog auditLog = AuditLog.CreateForUser(
            17,
            " secretary.disabled ",
            " ApplicationUser ",
            " 31 ",
            occurredAt,
            " policy violation ");

        Assert.Equal(17, auditLog.ActorUserId);
        Assert.Equal(AuditActorType.User, auditLog.ActorType);
        Assert.Equal("secretary.disabled", auditLog.Action);
        Assert.Equal("ApplicationUser", auditLog.EntityType);
        Assert.Equal("31", auditLog.EntityId);
        Assert.Equal("policy violation", auditLog.Reason);
        Assert.Equal(occurredAt, auditLog.OccurredAt);
    }

    [Fact]
    public void CreateForUserRejectsInvalidActor()
    {
        Assert.Throws<DomainException>(() => AuditLog.CreateForUser(
            0,
            "action",
            "entity",
            "1",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CreateForSystemDoesNotSetAUserActor()
    {
        AuditLog auditLog = AuditLog.CreateForSystem(
            "bootstrap",
            "ApplicationUser",
            "1",
            DateTimeOffset.UtcNow);

        Assert.Null(auditLog.ActorUserId);
        Assert.Equal(AuditActorType.System, auditLog.ActorType);
    }
}
