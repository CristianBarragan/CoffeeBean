using Foundgine;
using Foundgine.Abstractions;
using Foundgine.Semantics.Mutation;
using Foundgine.Semantics.Security.Execution;

namespace Foundgine.Security.Tests;

public sealed class MutationSecurityResourceLimitTests
{
    [Fact]
    public void Rejects_excessive_mutation_operation_count()
    {
        var request = new SemanticMutationRequest(
            new SemanticMutationOperationGraph(
                Enumerable.Range(0, 3).Select(_ => CreateOperation()).ToArray()));

        var limits = new SecurityResourceLimits { MaxMutationOperations = 2 };

        Assert.Throws<InvalidOperationException>(() =>
            SecurityResourceLimitValidator.Validate(request, limits));
    }

    [Fact]
    public void Rejects_excessive_fields_per_mutation()
    {
        var operation = CreateOperation() with
        {
            Fields = Enumerable.Range(0, 3)
                .Select(i => new SemanticMutationField(new FieldId($"f{i}"), i))
                .ToArray()
        };
        var request = new SemanticMutationRequest(new SemanticMutationOperationGraph([operation]));

        var limits = new SecurityResourceLimits { MaxMutationFieldsPerOperation = 2 };

        Assert.Throws<InvalidOperationException>(() =>
            SecurityResourceLimitValidator.Validate(request, limits));
    }

    [Fact]
    public void Rejects_excessive_dependencies()
    {
        var operation = CreateOperation() with
        {
            Dependencies = Enumerable.Range(0, 3)
                .Select(i => new SemanticMutationDependency(0, 0, new FieldId($"f{i}"), new FieldId($"t{i}")))
                .ToArray()
        };
        var request = new SemanticMutationRequest(new SemanticMutationOperationGraph([operation]));

        var limits = new SecurityResourceLimits { MaxMutationDependencies = 2 };

        Assert.Throws<InvalidOperationException>(() =>
            SecurityResourceLimitValidator.Validate(request, limits));
    }

    private static SemanticMutationOperation CreateOperation() => new(
        new EntityId("customer"),
        SemanticMutationKind.Create,
        [new SemanticMutationField(new FieldId("name"), "x")],
        null,
        [],
        [],
        [],
        []);
}
