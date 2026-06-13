using System.Text.RegularExpressions;
using FluentAssertions;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Enforces naming.guide.md §A conventions. These are not cosmetic: the return-shape rules in
/// <see cref="HandlerResultReturnTests"/> and <see cref="RepositoryReturnTypeTests"/> scope by the
/// <c>*Handler</c> / <c>*Repository</c> suffix, so a use-case class misnamed (e.g. a handler called
/// <c>*Service</c>) would silently slip past those checks. Keeping the suffix honest keeps the other
/// architecture rules effective.
/// </summary>
public class NamingConventionTests
{
    [Fact]
    public void Classes_ImplementingHandlerInterface_Should_BeNamedHandler()
    {
        var offenders = ConcreteClassesImplementing("^I.+Handler$")
            .Where(type => !type.Name.EndsWith("Handler"))
            .Select(type => type.FullName)
            .ToArray();

        offenders.Should().BeEmpty(
            because: "classes implementing I*Handler must be named *Handler. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Classes_ImplementingRepositoryInterface_Should_BeNamedRepository()
    {
        var offenders = ConcreteClassesImplementing("^I.+Repository$")
            .Where(type => !type.Name.EndsWith("Repository"))
            .Select(type => type.FullName)
            .ToArray();

        offenders.Should().BeEmpty(
            because: "classes implementing I*Repository must be named *Repository. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void FailureCodeHolders_Should_BeStatic_WithOnlyConstStringFields()
    {
        var offenders =
            (from type in ArchitectureRules.AllProductionTypes()
             where type.IsClass && type.Name.EndsWith("FailureCodes")
             let isStatic = type is { IsAbstract: true, IsSealed: true } // C# `static class` == abstract+sealed in IL
             let fieldsOk = type.GetFields().All(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
             where !isStatic || !fieldsOk
             select type.FullName).ToArray();

        offenders.Should().BeEmpty(
            because:
            "*FailureCodes must be static classes holding only `const string` codes "
            + "(exceptions.rule.md §E). Offenders: " + string.Join(", ", offenders));
    }

    private static IEnumerable<Type> ConcreteClassesImplementing(string interfaceNamePattern)
        => from type in ArchitectureRules.AllProductionTypes()
           where type is { IsClass: true, IsAbstract: false }
           where type.GetInterfaces().Any(i => Regex.IsMatch(i.Name, interfaceNamePattern))
           select type;
}
