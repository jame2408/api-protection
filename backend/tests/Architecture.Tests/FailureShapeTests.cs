using System.Reflection;
using ApiKeyManagement.SharedKernel.Domain;
using FluentAssertions;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Enforces ADR-004 §4: <c>Failure</c> stays a single-field <c>record Failure(string Code)</c>.
/// Diagnostic context (entity IDs, inputs, tenant scope) is captured by boundary loggers and never
/// carried on Failure — adding Message / Metadata / Detail / Context would both break the stable
/// wire contract and re-open the CLAUDE.md-vs-code conflict ADR-004 closed. This reflection lock
/// goes red the moment any extra public data member appears on the type.
/// </summary>
public class FailureShapeTests
{
    [Fact]
    public void Failure_Should_Expose_Only_The_Code_Property()
    {
        var properties = typeof(Failure)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => $"{p.PropertyType.Name} {p.Name}")
            .ToArray();

        properties.Should().BeEquivalentTo(
            ["String Code"],
            because:
            "Failure must stay `record Failure(string Code)` — diagnostic context belongs on boundary "
            + "loggers, not on Failure (ADR-004 §4). Found: " + string.Join(", ", properties));
    }

    [Fact]
    public void Failure_Should_Not_Declare_PublicFields()
    {
        var fields = typeof(Failure)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.Name)
            .ToArray();

        fields.Should().BeEmpty(
            because:
            "Failure carries no state beyond Code; a public field would smuggle extra data in "
            + "(ADR-004 §4). Found: " + string.Join(", ", fields));
    }
}
