using Foundgine.Metadata;

namespace Foundgine.Semantic.Inference;

/// <summary>
/// P2 (docs/CURRENT-STATUS.md: "semantic mapping simplification"):
/// builds a <see cref="SemanticEntity"/>'s <i>structural</i> shape --
/// identity, fields, relationships -- directly from the
/// <see cref="EntityMetadata"/>/<see cref="RelationshipMetadata"/> an
/// application already registers for query/mutation planning, instead of
/// asking a developer to re-declare the same names, ids, and types a
/// second time by hand through <see cref="SemanticEntityBuilder"/>.
///
/// The governing rule (from the same discussion this class implements):
///
/// <code>
/// Convention first. Configuration only where semantics cannot be inferred.
/// </code>
///
/// So a Banking semantic model that previously had to spell out every
/// field and relationship becomes just the part that's actually new
/// information:
///
/// <code>
/// var model = new SemanticModelBuilder()
///     .InferEntity(BankingMetadata.Customer, relationships, customer =&gt; customer
///         .Search(new SearchCapability([new FieldId(2)], SearchStrategy.Fuzzy)))
///     .InferEntity(BankingMetadata.Account, relationships)
///     .InferEntity(BankingMetadata.Transaction, relationships)
///     .Build();
/// </code>
///
/// <b>What is inferred</b> (structure -- "what exists and how it's
/// related"):
///
/// <list type="bullet">
/// <item><description>Identity -- the column conventionally named "Id", the same convention <c>Foundgine.Planning.MutationPlanner.IsConventionalPrimaryKey</c> already relies on.</description></item>
/// <item><description>Fields -- every other column on the entity.</description></item>
/// <item><description>Relationships -- every <see cref="RelationshipMetadata"/> whose <see cref="RelationshipMetadata.Source"/> is this entity.</description></item>
/// </list>
///
/// <b>What is <i>not</i> inferred</b> (business meaning -- "what it means
/// to a human or an agent"), and stays an honest, explicit gap rather
/// than a guess:
///
/// <list type="bullet">
/// <item><description>Field CLR types -- <see cref="ColumnMetadata"/> carries no type today. Defaults to <see cref="DefaultFieldType"/> (<c>string</c>) unless overridden via <paramref name="fieldTypes"/> on <see cref="InferEntity"/>. A future Roslyn/reflection-backed source (Milestone 10) reading the real CLR domain type is expected to close this for good -- this inference is deliberately not that generator, per the roadmap's "postpone the Roslyn generator" advice: the manual/inferred path has to prove the semantic model is valuable first.</description></item>
/// <item><description>Relationship cardinality -- direction alone doesn't say whether "Account -&gt; Customer" is one or many. Defaults to <see cref="RelationshipCardinality.Many"/> unless overridden via <paramref name="cardinalityOverrides"/>.</description></item>
/// <item><description>Search capability, aliases, actions, and policies -- purely business semantics, never inferable from a column list. Added afterwards via <paramref name="configure"/>, exactly the way a hand-authored entity adds them today.</description></item>
/// </list>
/// </summary>
public static class SemanticModelInference
{
    /// <summary>
    /// Default CLR type assigned to an inferred field whose real type
    /// isn't supplied via <c>fieldTypes</c>. See the type-level remarks
    /// for why this gap exists and how to close it per-field.
    /// </summary>
    public static readonly Type DefaultFieldType = typeof(string);

    /// <summary>
    /// Infers one entity's identity, fields, and outgoing relationships
    /// from <paramref name="entity"/> and <paramref name="relationships"/>,
    /// then lets <paramref name="configure"/> add whatever structure can't
    /// supply -- search, actions, policies.
    /// </summary>
    /// <param name="builder">The model under construction.</param>
    /// <param name="entity">The already-registered physical entity metadata to infer structure from.</param>
    /// <param name="relationships">Every relationship in the domain; only those whose <see cref="RelationshipMetadata.Source"/> matches <paramref name="entity"/> are attached.</param>
    /// <param name="configure">Optional business-semantics overlay -- search, actions, policies. Runs after inference, so it may also add fields/relationships inference didn't cover.</param>
    /// <param name="fieldTypes">Optional per-column CLR type overrides, keyed by <see cref="ColumnMetadata.Name"/>, for columns whose type isn't <see cref="DefaultFieldType"/>.</param>
    /// <param name="cardinalityOverrides">Optional per-relationship cardinality overrides, keyed by <see cref="RelationshipMetadata.Name"/>, for relationships that aren't <see cref="RelationshipCardinality.Many"/>.</param>
    public static SemanticModelBuilder InferEntity(
        this SemanticModelBuilder builder,
        EntityMetadata entity,
        IReadOnlyList<RelationshipMetadata> relationships,
        Action<SemanticEntityBuilder>? configure = null,
        IReadOnlyDictionary<string, Type>? fieldTypes = null,
        IReadOnlyDictionary<string, RelationshipCardinality>? cardinalityOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(relationships);

        return builder.Entity(entity.EntityId, entity.Name, semantic =>
        {
            var identityColumn = entity.Columns.FirstOrDefault(
                c => string.Equals(c.Name, "Id", StringComparison.Ordinal));

            if (identityColumn is null)
            {
                throw new InvalidOperationException(
                    $"Cannot infer a semantic identity for '{entity.Name}': no column named 'Id' " +
                    "was found. Inference relies on the same 'Id' convention " +
                    "Foundgine.Planning.MutationPlanner already uses -- an entity that doesn't " +
                    $"follow it must be declared by hand via {nameof(SemanticEntityBuilder)} instead.");
            }

            semantic.Identity(new FieldId(identityColumn.Id.Value), identityColumn.Name);

            foreach (var column in entity.Columns)
            {
                if (ReferenceEquals(column, identityColumn))
                    continue;

                var fieldType = fieldTypes is not null && fieldTypes.TryGetValue(column.Name, out var declaredType)
                    ? declaredType
                    : DefaultFieldType;

                semantic.Field(new FieldId(column.Id.Value), column.Name, fieldType);
            }

            foreach (var relationship in relationships)
            {
                if (relationship.Source != entity.EntityId)
                    continue;

                var cardinality =
                    cardinalityOverrides is not null &&
                    cardinalityOverrides.TryGetValue(relationship.Name, out var declaredCardinality)
                        ? declaredCardinality
                        : RelationshipCardinality.Many;

                semantic.Relationship(relationship.Id, relationship.Name, relationship.Target, cardinality);
            }

            configure?.Invoke(semantic);
        });
    }

    /// <summary>
    /// Infers every entity in <paramref name="entities"/> in one call, with a shared per-entity
    /// configuration callback. Use <see cref="InferEntity"/> directly for entities that need their
    /// own <c>fieldTypes</c>/<c>cardinalityOverrides</c>.
    /// </summary>
    public static SemanticModelBuilder InferAll(
        this SemanticModelBuilder builder,
        IReadOnlyList<EntityMetadata> entities,
        IReadOnlyList<RelationshipMetadata> relationships,
        Action<EntityMetadata, SemanticEntityBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(relationships);

        foreach (var entity in entities)
            builder.InferEntity(entity, relationships, semantic => configure?.Invoke(entity, semantic));

        return builder;
    }
}
