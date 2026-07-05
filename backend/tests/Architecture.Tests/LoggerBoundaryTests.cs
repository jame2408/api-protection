using FluentAssertions;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Enforces CLAUDE.md / ADR-004 / di.rule.md §F: NEVER inject ILogger into Service, Domain, or
/// Handler layers. Diagnostic context is captured at the boundary (Endpoint, Middleware, Pipeline
/// Behavior, Background Service) and by Infrastructure ApiClients (exceptions.rule.md §B explicitly
/// allows ILogger there). The forbidden zones are identified by namespace (<c>.Domain</c> /
/// <c>.Application</c>) and by Handler name; Host and Infrastructure assemblies are out of scope by
/// construction (Host isn't referenced here; Infrastructure types live in <c>.Infrastructure.*</c>).
/// </summary>
public class LoggerBoundaryTests
{
    [Fact]
    public void ServiceDomainAndHandlerTypes_ShouldNot_InjectLogger()
    {
        var offenders =
            (from type in ArchitectureRules.AllProductionTypes()
             where type.IsClass
                   && (type.Namespace is not null
                       && (type.Namespace.Contains(".Domain") || type.Namespace.Contains(".Application"))
                       || type.Name.EndsWith("Handler", StringComparison.Ordinal))
             where ArchitectureRules.InjectsLogger(type)
             select type.FullName).ToArray();

        offenders.Should().BeEmpty(
            because:
            "Service/Domain/Handler must not inject ILogger — logging belongs at the boundary "
            + "(di.rule.md §F). Offending types: " + string.Join(", ", offenders));
    }
}
