using FluentAssertions;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Enforces exceptions.rule.md §B: Repository methods return raw types (domain entity, primitive,
/// collection, Task) and NEVER <c>Result&lt;T, Failure&gt;</c>. Wrapping DB outcomes into business
/// Failures is the Application layer's job; a Repository that returns Result leaks that boundary.
/// </summary>
public class RepositoryReturnTypeTests
{
    [Fact]
    public void RepositoryInterfaces_ShouldNot_ReturnResult()
    {
        var offenders =
            (from type in ArchitectureRules.AllProductionTypes()
             where type.IsInterface && type.Name.StartsWith('I') && type.Name.EndsWith("Repository")
             from method in type.GetMethods()
             where ArchitectureRules.ReturnsResult(method)
             select $"{type.Name}.{method.Name}").ToArray();

        offenders.Should().BeEmpty(
            because:
            "Repository methods return raw types; Result<T, Failure> belongs to the Application layer "
            + "(exceptions.rule.md §B). Offending members: " + string.Join(", ", offenders));
    }
}
