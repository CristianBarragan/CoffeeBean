using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// Entry point for turning a domain -- today, hand-authored; later,
/// possibly generated -- into a <see cref="SemanticModel"/>.
///
/// <code>
/// var model = new SemanticModelBuilder()
///     .Entity(customerId, "Customer", customer => customer
///         .Identity(idField, "Id")
///         .Field(nameField, "Name", typeof(string))
///         .Relationship(accountsRel, "Accounts", accountId, RelationshipCardinality.Many))
///     .Build();
/// </code>
/// </summary>
public sealed class SemanticModelBuilder
{
    private readonly SemanticModel _model = new();

    public SemanticModelBuilder Entity(EntityId id, string name, Action<SemanticEntityBuilder> configure)
    {
        var builder = new SemanticEntityBuilder(id, name);
        configure(builder);
        _model.Register(builder.Build());
        return this;
    }

    public SemanticModel Build() => _model;
}
