namespace ApiKeyManagement.TestInfrastructure;

/// <summary>Freezes GetUtcNow() at construction time so expiry-boundary scenarios
/// can hit exact guard equalities (frozen instant shared with the host via DI).</summary>
public sealed class FrozenTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _frozen = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _frozen;
}
