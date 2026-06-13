using System.Reflection;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.Architecture.Tests;

/// <summary>
/// Shared reflection helpers for the return-type architecture rules. NetArchTest checks
/// IL-level dependencies (used by <see cref="BoundedContextIsolationTests"/>) but cannot inspect
/// method return types, so the Repository / Handler return-shape rules use reflection here.
/// </summary>
internal static class ArchitectureRules
{
    // SharedKernel + bounded contexts + Infrastructure: every production assembly the
    // architecture rules inspect (repository interfaces, handlers, failure-code holders).
    private static readonly string[] ProductionAssemblies =
    [
        "ApiKeyManagement.SharedKernel",
        "ApiKeyManagement.AccessPolicy",
        "ApiKeyManagement.Audit",
        "ApiKeyManagement.KeyLifecycle",
        "ApiKeyManagement.Monitoring",
        "ApiKeyManagement.TenantManagement",
        "ApiKeyManagement.Infrastructure",
    ];

    public static IEnumerable<Type> AllProductionTypes()
        => ProductionAssemblies
            .Select(name => Assembly.Load(new AssemblyName(name)))
            .SelectMany(assembly => assembly.GetTypes());

    /// <summary>True when the method returns <c>Result&lt;,&gt;</c> or <c>Task&lt;Result&lt;,&gt;&gt;</c>.</summary>
    public static bool ReturnsResult(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (IsResult(returnType))
        {
            return true;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return IsResult(returnType.GetGenericArguments()[0]);
        }

        return false;
    }

    /// <summary>True for <c>Task</c> or <c>Task&lt;T&gt;</c> returns (the async surface a Handler exposes).</summary>
    public static bool ReturnsTaskLike(MethodInfo method)
    {
        var returnType = method.ReturnType;
        return returnType == typeof(Task)
            || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));
    }

    private static bool IsResult(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<,>);

    /// <summary>
    /// True when any constructor of <paramref name="type"/> takes an <c>ILogger</c> or
    /// <c>ILogger&lt;T&gt;</c> parameter. Detected by name + namespace so the test project needs no
    /// Microsoft.Extensions.Logging package reference. Covers primary constructors (their parameters
    /// surface as constructor parameters via reflection).
    /// </summary>
    public static bool InjectsLogger(Type type)
        => type.GetConstructors().Any(ctor => ctor.GetParameters().Any(p => IsLogger(p.ParameterType)));

    private static bool IsLogger(Type type)
        => type.Namespace == "Microsoft.Extensions.Logging"
           && (type.Name == "ILogger" || type.Name == "ILogger`1");
}
