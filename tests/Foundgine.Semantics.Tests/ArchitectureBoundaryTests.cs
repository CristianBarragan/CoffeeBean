using System.Reflection;
using Xunit;

namespace Foundgine.Semantics.Tests;

/// <summary>
/// Compiler/build-output driven architecture invariants.
/// These tests inspect the actual compiled assembly references rather than
/// grepping source text, so string literals such as "Npgsql" are not false positives.
/// </summary>
public sealed class ArchitectureBoundaryTests
{
    [Theory]
    [InlineData("Foundgine.Abstractions")]
    [InlineData("Foundgine.Metadata")]
    [InlineData("Foundgine.Semantics")]
    [InlineData("Foundgine.Planning")]
    public void Provider_independent_assemblies_do_not_reference_sql_provider_assemblies(
        string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var forbidden = assembly
            .GetReferencedAssemblies()
            .Where(x =>
                x.Name is not null &&
                (x.Name.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                 x.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
                 x.Name.Equals("Dapper", StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Name!)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            forbidden.Length == 0,
            $"{assemblyName} has forbidden provider references: {string.Join(", ", forbidden)}");
    }
}
