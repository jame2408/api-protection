using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Enforces CLAUDE.md "NEVER add direct BC-to-BC references — only via SharedKernel interfaces"
/// and ADR-003. Cross-BC collaboration goes through interfaces in SharedKernel/Contracts
/// (see naming.guide.md §A, exceptions.rule.md "跨 BC Contract 例外"). SharedKernel is the only
/// shared path; a BC referencing another BC's namespace directly is a violation.
/// </summary>
public class BoundedContextIsolationTests
{
    private static readonly string[] BoundedContexts =
    [
        "ApiKeyManagement.AccessPolicy",
        "ApiKeyManagement.Audit",
        "ApiKeyManagement.KeyLifecycle",
        "ApiKeyManagement.Monitoring",
        "ApiKeyManagement.TenantManagement",
    ];

    [Theory]
    [MemberData(nameof(EachBoundedContext))]
    public void BoundedContext_ShouldNot_DependOn_OtherBoundedContexts(string boundedContext)
    {
        var others = BoundedContexts.Where(bc => bc != boundedContext).ToArray();
        var assembly = Assembly.Load(new AssemblyName(boundedContext));

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespace(boundedContext)
            .ShouldNot().HaveDependencyOnAny(others)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because:
            $"{boundedContext} must reach other BCs only through SharedKernel/Contracts (ADR-003). " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    public static IEnumerable<object[]> EachBoundedContext()
        => BoundedContexts.Select(bc => new object[] { bc });
}
