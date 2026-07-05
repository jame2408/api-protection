using System.Reflection;
using FluentAssertions;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Enforces exceptions.rule.md §A: BC-internal use-case Handlers must return
/// <c>Task&lt;Result&lt;T, Failure&gt;&gt;</c> and never throw for business logic.
/// Cross-BC contracts (SharedKernel/Contracts) are exempt — but those are implemented by
/// <c>*Service</c> classes returning contract DTOs (e.g. IAccessPolicyService → Task&lt;Guid&gt;,
/// IConsumerValidator → Task&lt;ConsumerValidationResult&gt;), not <c>*Handler</c>. Scoping this rule
/// to concrete <c>*Handler</c> types keeps the carve-out clean without special-casing contracts.
/// </summary>
public class HandlerResultReturnTests
{
    [Fact]
    public void HandlerPublicAsyncMethods_Should_ReturnResult()
    {
        var offenders =
            (from type in ArchitectureRules.AllProductionTypes()
             where type is { IsClass: true, IsAbstract: false } && type.Name.EndsWith("Handler", StringComparison.Ordinal)
             from method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
             where ArchitectureRules.ReturnsTaskLike(method) && !ArchitectureRules.ReturnsResult(method)
             select $"{type.Name}.{method.Name}").ToArray();

        offenders.Should().BeEmpty(
            because:
            "BC-internal Handlers must return Task<Result<T, Failure>> (exceptions.rule.md §A). "
            + "Offending members: " + string.Join(", ", offenders));
    }
}
