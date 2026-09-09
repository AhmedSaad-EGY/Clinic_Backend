using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Common;

public sealed class EntityTests
{
    [Fact]
    public void ClearDomainEventsRemovesRaisedEvents()
    {
        TestAggregate aggregate = new();
        aggregate.EmitEvent();

        Assert.Single(aggregate.DomainEvents);

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public void EmitEvent() => RaiseDomainEvent(new TestDomainEvent(DateTimeOffset.UtcNow));
    }

    private sealed record TestDomainEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
