using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Enforces CLAUDE.md "NEVER add direct BC-to-BC references — only via SharedKernel interfaces"
/// and ADR-003. Cross-BC collaboration goes through interfaces in SharedKernel/Contracts
/// (see naming.guide.md §A, exceptions.rule.md "跨 BC Contract 例外"). SharedKernel is the only
/// shared path; a BC referencing another BC's namespace directly is a violation.
/// BC list is discovered dynamically by scanning backend/src/ so a newly added BC is auto-enrolled;
/// if its ProjectReference is missing from this test project, Assembly.Load fails loudly below.
/// </summary>
public class BoundedContextIsolationTests
{
    private static readonly string[] KnownNonBoundedContextAssemblies =
    [
        "ApiKeyManagement.SharedKernel",
        "ApiKeyManagement.Infrastructure",
        "ApiKeyManagement.Api",
    ];

    private static readonly string[] KnownMinimumBoundedContexts =
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
        var others = DiscoverBoundedContexts().Where(bc => bc != boundedContext).ToArray();

        Assembly assembly;
        try
        {
            assembly = Assembly.Load(new AssemblyName(boundedContext));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load assembly '{boundedContext}' (discovered by scanning backend/src/). " +
                "If this is a newly added BC, add a <ProjectReference> to its csproj in " +
                "Architecture.Tests.csproj so the dll is copied to the test output directory.",
                ex);
        }

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespace(boundedContext)
            .ShouldNot().HaveDependencyOnAny(others)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because:
            $"{boundedContext} must reach other BCs only through SharedKernel/Contracts (ADR-003). " +
            $"Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void DiscoverBoundedContexts_ShouldInclude_KnownMinimumSet()
    {
        var discovered = DiscoverBoundedContexts();

        discovered.Should().Contain(KnownMinimumBoundedContexts,
            because: "this guards against the discovery logic silently returning an empty or " +
                "partial set; if a BC was intentionally removed from backend/src/, update " +
                "KnownMinimumBoundedContexts accordingly");
    }

    public static IEnumerable<object[]> EachBoundedContext()
        => DiscoverBoundedContexts().Select(bc => new object[] { bc });

    private static string[] DiscoverBoundedContexts()
    {
        var backendDirectory = FindBackendDirectory(AppContext.BaseDirectory);
        var srcDirectory = Path.Combine(backendDirectory, "src");

        return Directory.GetDirectories(srcDirectory)
            .SelectMany(dir => Directory.GetFiles(dir, "*.csproj"))
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && !KnownNonBoundedContextAssemblies.Contains(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindBackendDirectory(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.GetFiles("ApiKeyManagement.slnx").Length > 0)
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not find backend/ (containing ApiKeyManagement.slnx) by walking up from '{startDirectory}'.");
    }
}
