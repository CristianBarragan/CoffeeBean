using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Security;

namespace Foundgine.Semantics.Capabilities;

/// <summary>
/// Canonical, machine-readable description of the semantic application surface.
/// The contract is descriptive and never replaces execution-time authorization.
/// </summary>
public sealed record SemanticCapabilityContract(
    int Version,
    IReadOnlyList<SemanticCapability> Capabilities);

/// <summary>
/// A named capability exposed by the semantic model.
/// </summary>
/// <summary>Static implementation binding for a semantic capability.</summary>
public sealed record SemanticCapabilityImplementation(
    string TypeName,
    string MethodName);

/// <summary>Provider-neutral descriptive metadata for a semantic capability.</summary>
public sealed record SemanticCapabilityMetadata(
    string? Description = null,
    IReadOnlyDictionary<string, string>? Properties = null);

/// <summary>
/// Well-known values for <see cref="SemanticCapability.Operation"/>.
///
/// <see cref="SemanticCapability.Operation"/> is deliberately a string, not
/// an enum: a capability's operation name is an open, application-defined
/// identifier (e.g. "transferFunds", "advance_fulfillment"), not a closed
/// CRUD set. These constants exist so the built-in read/write/mutation/
/// traversal operations that Foundgine itself generates are always spelled
/// identically everywhere they're compared, instead of each call site
/// hand-typing the literal and risking a silent mismatch.
/// </summary>
public static class SemanticCapabilityOperations
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Upsert = "upsert";
    public const string Traverse = "traverse";

    /// <summary>Maps a <see cref="Mutation.SemanticMutationKind"/> to its capability operation string.</summary>
    public static string From(Mutation.SemanticMutationKind kind) => kind switch
    {
        Mutation.SemanticMutationKind.Create => Create,
        Mutation.SemanticMutationKind.Update => Update,
        Mutation.SemanticMutationKind.Delete => Delete,
        Mutation.SemanticMutationKind.Upsert => Upsert,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unrecognized semantic mutation kind.")
    };
}

/// <summary>Write-action kinds used when generating mutation capabilities. Internal — see <see cref="SemanticCapabilityOperations"/> for the corresponding public operation strings.</summary>
internal enum SemanticCapabilityWriteAction
{
    Create,
    Update,
    Delete,
    Upsert
}

public sealed record SemanticCapability(
    string Id,
    string Name,
    EntityId TargetEntityId,
    AuthorizationDecision Access,
    IReadOnlyList<SemanticCapabilityInput> Inputs,
    IReadOnlyList<SemanticCapabilityConstraint> Constraints,
    IReadOnlyList<SemanticCapabilityEffect> Effects,
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> Relationships)
{
    /// <summary>Canonical semantic action represented by this capability.</summary>
    /// <summary>Named semantic schema that owns this capability.</summary>
    public string Schema { get; init; } = string.Empty;

    public string Operation { get; init; } = "read";

    /// <summary>Whether executing the action may mutate application state.</summary>
    public bool HasSideEffects { get; init; }

    /// <summary>Whether repeated execution is semantically safe without an idempotency key.</summary>
    public bool IsIdempotent { get; init; }

    /// <summary>Semantic compatibility version of this capability definition.</summary>
    public int Version { get; init; } = SemanticVersionSet.CurrentCapabilityVersion;

    /// <summary>Optional static application implementation bound to this capability.</summary>
    public SemanticCapabilityImplementation? Implementation { get; init; }

    /// <summary>Provider-neutral metadata carried by all capability projections.</summary>
    public SemanticCapabilityMetadata Metadata { get; init; } = new();

    /// <summary>Fully qualified semantic name used by downstream projections.</summary>
    public string QualifiedName => string.IsNullOrEmpty(Schema) ? Id : $"{Schema}.{Id}";

    /// <summary>Security guarantees that planners/providers must preserve.</summary>
    public IReadOnlyList<string> RequiredSecurityInvariants { get; init; } = [];

    /// <summary>
    /// Declarative authorization requirements associated with this capability.
    ///
    /// These requirements describe what execution-time authorization must
    /// establish. They are not authorization decisions and contain no
    /// request-specific authorization state.
    /// </summary>
    public IReadOnlyList<SemanticCapabilityAuthorizationRequirement> AuthorizationRequirements
    {
        get;
        init;
    } = [];

    /// <summary>Returns the canonical invariant set when callers did not explicitly supply one.</summary>
    public IReadOnlyList<string> EffectiveSecurityInvariants => RequiredSecurityInvariants.Count > 0
        ? RequiredSecurityInvariants
        : SemanticCapabilitySecurityDefaults.For(this);
}

/// <summary>
/// Describes one input accepted by a semantic capability.
/// </summary>
public sealed record SemanticCapabilityInput(
    string Name,
    string Type,
    bool Required,
    string? Description = null);

/// <summary>
/// Describes a semantic precondition or execution constraint.
/// </summary>
public sealed record SemanticCapabilityConstraint(
    string Name,
    string Description);

/// <summary>
/// Describes a side effect that may result from executing a capability.
/// </summary>
public sealed record SemanticCapabilityEffect(
    string Name,
    string Description);

/// <summary>
/// Builds the first canonical capability contract from the existing semantic
/// model and authorization capability surface. Providers and transports should
/// consume this contract rather than constructing their own semantic schemas.
/// </summary>
public static class SemanticCapabilityContractDiscovery
{
    public const int CurrentVersion = 1;

    public static SemanticCapabilityContract Describe(
        SemanticModel model,
        ISemanticAuthorizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(policy);

        var discovered = SemanticAuthorizationCapabilityDiscovery.Describe(model, policy);
        // Capability discovery intentionally hides authorization predicates, but
        // the canonical semantic contract must retain the exact policy predicate
        // for planning/security consumers that need to carry it forward.
        // Rehydrate only the top-level entity decision here; the descriptive
        // authorization capability surface remains predicate-free.
        var capabilities = discovered.Entities
            .Select(entity => entity with
            {
                Read = PreservePredicate(entity.Read, policy.GetPredicate(entity.EntityId, AuthorizationOperation.Read)),
                Write = PreservePredicate(entity.Write, policy.GetPredicate(entity.EntityId, AuthorizationOperation.Write))
            })
            .SelectMany(entity => BuildCapabilities(model, entity))
            .OrderBy(capability => capability.Id, StringComparer.Ordinal)
            .ToArray();

        return new SemanticCapabilityContract(CurrentVersion, capabilities);
    }


    private static AuthorizationDecision PreservePredicate(
        AuthorizationDecision decision,
        AuthorizationPredicate? predicate) =>
        predicate is not null
            ? AuthorizationDecision.Conditional(predicate)
            : decision;

    private static IEnumerable<SemanticCapability> BuildCapabilities(
        SemanticModel model,
        SemanticAuthorizationCapability entity)
    {
        yield return new SemanticCapability(
            Id: $"{entity.Name}.read",
            Name: $"Read {entity.Name}",
            TargetEntityId: entity.EntityId,
            Access: entity.Read,
            Inputs: [],
            Constraints: [],
            Effects: [],
            Fields: entity.Fields
                .Where(x => x.Read.IsAllowed)
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Relationships: entity.Relationships
                .Where(x => x.Read.IsAllowed)
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray())
        {
            Operation = SemanticCapabilityOperations.Read,
            HasSideEffects = false,
            IsIdempotent = true
        };

        yield return new SemanticCapability(
            Id: $"{entity.Name}.write",
            Name: $"Write {entity.Name}",
            TargetEntityId: entity.EntityId,
            Access: entity.Write,
            Inputs: BuildWriteInputs(model, entity),
            Constraints: BuildWriteConstraints(),
            Effects: entity.Write.IsAllowed
                ? [new SemanticCapabilityEffect(
                    "data.write",
                    $"May modify {entity.Name} data when execution-time authorization permits it.")]
                : [],
            Fields: entity.Fields
                .Where(x => x.Write.IsAllowed)
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Relationships: entity.Relationships
                .Where(x => x.Write.IsAllowed)
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray())
        {
            Operation = SemanticCapabilityOperations.Write,
            HasSideEffects = entity.Write.IsAllowed,
            IsIdempotent = false
        };

        if (entity.Write.IsAllowed)
        {
            foreach (var action in BuildMutationActions(model, entity))
                yield return action;
        }

        foreach (var relationship in entity.Relationships.Where(x => x.Read.IsAllowed))
        {
            yield return new SemanticCapability(
                Id: $"{entity.Name}.{relationship.Name}.traverse",
                Name: $"Traverse {entity.Name}.{relationship.Name}",
                TargetEntityId: relationship.TargetEntityId,
                Access: relationship.Read,
                Inputs: [],
                Constraints: [],
                Effects: [],
                Fields: [],
                Relationships: [])
            {
                Operation = SemanticCapabilityOperations.Traverse,
                HasSideEffects = false,
                IsIdempotent = true
            };
        }
    }


    private static IReadOnlyList<SemanticCapabilityConstraint> BuildWriteConstraints() =>
    [
        new("authorization", "Execution-time authorization must permit the requested mutation."),
        new("writable-fields", "Every requested field must be writable under the effective authorization policy.")
    ];

    private static IEnumerable<SemanticCapability> BuildMutationActions(
        SemanticModel model,
        SemanticAuthorizationCapability entity)
    {
        // Iterating the enum (rather than a string array) means the compiler
        // flags every switch below (CS8509) if a new SemanticCapabilityWriteAction
        // member isn't handled, instead of silently falling through to an
        // empty constraint/effect list at runtime as the old string-keyed
        // switches with a "_ => ..." catch-all did.
        foreach (var action in Enum.GetValues<SemanticCapabilityWriteAction>())
        {
            IReadOnlyList<SemanticCapabilityConstraint> constraints = action switch
            {
                SemanticCapabilityWriteAction.Create =>
                [
                    new SemanticCapabilityConstraint("writable-fields", "Every supplied field must be writable.")
                ],
                SemanticCapabilityWriteAction.Update =>
                [
                    new SemanticCapabilityConstraint("target-selection", "A target filter or equivalent identity selection is required."),
                    new SemanticCapabilityConstraint("writable-fields", "Every supplied field must be writable.")
                ],
                SemanticCapabilityWriteAction.Delete =>
                [
                    new SemanticCapabilityConstraint("target-selection", "A target filter or equivalent identity selection is required.")
                ],
                SemanticCapabilityWriteAction.Upsert =>
                [
                    new SemanticCapabilityConstraint("conflict-key", "A conflict key or equivalent identity must determine the upsert target."),
                    new SemanticCapabilityConstraint("writable-fields", "Every supplied field must be writable.")
                ],
                _ => throw new ArgumentOutOfRangeException()
            };

            var operation = action switch
            {
                SemanticCapabilityWriteAction.Create => SemanticCapabilityOperations.Create,
                SemanticCapabilityWriteAction.Update => SemanticCapabilityOperations.Update,
                SemanticCapabilityWriteAction.Delete => SemanticCapabilityOperations.Delete,
                SemanticCapabilityWriteAction.Upsert => SemanticCapabilityOperations.Upsert,
                _ => throw new ArgumentOutOfRangeException()
            };

            var effects = new List<SemanticCapabilityEffect>
            {
                new($"data.{operation}", $"May {operation} {entity.Name} data when execution-time authorization permits it.")
            };

            if (action is SemanticCapabilityWriteAction.Create or SemanticCapabilityWriteAction.Update or SemanticCapabilityWriteAction.Upsert)
                effects.Add(new SemanticCapabilityEffect("field.mutation", "May change writable field values."));

            yield return new SemanticCapability(
                Id: $"{entity.Name}.{operation}",
                Name: $"{action} {entity.Name}",
                TargetEntityId: entity.EntityId,
                Access: entity.Write,
                Inputs: action == SemanticCapabilityWriteAction.Delete ? [] : BuildWriteInputs(model, entity),
                Constraints: constraints,
                Effects: effects,
                Fields: entity.Fields.Where(x => x.Write.IsAllowed).Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                Relationships: entity.Relationships.Where(x => x.Write.IsAllowed).Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray())
            {
                Operation = operation,
                HasSideEffects = entity.Write.IsAllowed,
                IsIdempotent = action is SemanticCapabilityWriteAction.Update or SemanticCapabilityWriteAction.Delete or SemanticCapabilityWriteAction.Upsert
            };
        }
    }

    private static IReadOnlyList<SemanticCapabilityInput> BuildWriteInputs(
        SemanticModel model,
        SemanticAuthorizationCapability entity)
    {
        var writableFields = entity.Fields
            .Where(x => x.Write.IsAllowed)
            .Select(field => new SemanticCapabilityInput(
                field.Name,
                field.ClrTypeName(model, entity.EntityId),
                Required: false,
                Description: $"Writable field on {entity.Name}."))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return writableFields;
    }

    private static string ClrTypeName(
        this SemanticFieldAuthorizationCapability field,
        SemanticModel model,
        EntityId entityId)
    {
        var semanticField = model.Get(entityId).Fields.Single(x => x.Id == field.FieldId);
        return semanticField.ClrType.FullName ?? semanticField.ClrType.Name;
    }
}

