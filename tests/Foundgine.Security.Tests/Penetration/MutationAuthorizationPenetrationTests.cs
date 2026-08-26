using Foundgine.Abstractions;
using Foundgine.Planning.Mutation;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Query;
using Xunit;

namespace Foundgine.Security.Tests.Penetration;

/// <summary>Hostile mutation-plan tests: hidden fields, unauthorized filters and relationships.</summary>
public sealed class MutationAuthorizationPenetrationTests
{
    [Fact]
    public void Unauthorized_write_field_is_rejected()
    {
        var entity = Entity(1, "Account", (1, 11), (2, 12));
        var schema = new TestSchema(entity);
        var plan = new MutationPlan([new MutationOperation(
            entity,
            MutationKind.Update,
            [new MutationFieldValue(new ColumnId(12), "attacker")],
            null)]);

        var exception = Assert.Throws<SemanticAuthorizationException>(() =>
            new MutationAuthorizer(schema, new AllowOnlyFieldPolicy(new FieldId(1))).Authorize(plan));

        Assert.Contains("field", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unauthorized_return_field_is_rejected()
    {
        var entity = Entity(1, "Account", (1, 11), (2, 12));
        var schema = new TestSchema(entity);
        var plan = new MutationPlan([new MutationOperation(
            entity,
            MutationKind.Update,
            [],
            null,
            ReturnFields: [new FieldId(2)])]);

        Assert.Throws<SemanticAuthorizationException>(() =>
            new MutationAuthorizer(schema, new AllowOnlyFieldPolicy(new FieldId(1))).Authorize(plan));
    }

    [Fact]
    public void Unauthorized_filter_field_is_rejected()
    {
        var entity = Entity(1, "Account", (1, 11), (2, 12));
        var schema = new TestSchema(entity);
        var filter = new SemanticFieldFilter(new FieldId(2), SemanticFilterOperator.Eq, "victim");
        var plan = new MutationPlan([new MutationOperation(entity, MutationKind.Delete, [], filter)]);

        Assert.Throws<SemanticAuthorizationException>(() =>
            new MutationAuthorizer(schema, new AllowOnlyFieldPolicy(new FieldId(1))).Authorize(plan));
    }

    private static MutationEntitySchema Entity(int id, string name, params (int Field, int Column)[] fields)
    {
        var map = fields.ToDictionary(x => new FieldId((ushort)x.Field), x => (ColumnId?)new ColumnId((ushort)x.Column));
        var columns = map.Values.Select(x => x!.Value).ToHashSet();
        return new MutationEntitySchema(new EntityId((ushort)id), name, columns, map, columns.First());
    }

    private sealed class AllowOnlyFieldPolicy(FieldId allowed) : AllowAllSemanticAuthorizationPolicy
    {
        public override bool CanWriteEntity(EntityId entityId) => true;
        public override bool CanWriteField(EntityId entityId, FieldId fieldId) => fieldId == allowed;
        public override bool CanAccessField(EntityId entityId, FieldId fieldId) => fieldId == allowed;
    }

    private sealed class TestSchema(MutationEntitySchema entity) : IMutationSchema
    {
        public MutationEntitySchema GetEntity(EntityId entityId) => entity.Id == entityId ? entity : throw new KeyNotFoundException();
        public MutationRelationshipSchema GetRelationship(RelationshipId relationshipId) => throw new KeyNotFoundException();
    }
}
