using System.Security.Claims;

namespace ApiKeyManagement.SharedKernel.Domain;

// ADR-024 §3: Actor identifies who triggered a control-plane action (User or System),
// threaded explicitly endpoint -> Command -> handler -> domain -> event (no ambient context).
// Only System.Security.Claims (BCL) is referenced here — SharedKernel must not pick up an
// ASP.NET Core package dependency just to map claims.
public enum ActorType
{
    User,
    System
}

public record Actor(ActorType Type, string Id, string Name)
{
    // ADR-024 §2: role="System" identifies the internal System actor; every other role maps
    // to User. `name` claim is optional display name, falling back to `sub` when absent.
    public static Actor FromClaims(ClaimsPrincipal principal)
    {
        var id = principal.FindFirst("sub")?.Value ?? string.Empty;
        var role = principal.FindFirst("role")?.Value;
        var type = role == "System" ? ActorType.System : ActorType.User;
        var name = principal.FindFirst("name")?.Value ?? id;

        return new Actor(type, id, name);
    }
}
