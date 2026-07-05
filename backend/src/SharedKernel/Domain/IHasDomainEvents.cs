namespace ApiKeyManagement.SharedKernel.Domain;

/// <summary>
/// Non-generic domain event seam: lets Infrastructure (which cannot depend on
/// each aggregate's TId) discover and harvest events via ChangeTracker without
/// a generic type parameter. Implemented by <see cref="AggregateRoot{TId}"/>.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
