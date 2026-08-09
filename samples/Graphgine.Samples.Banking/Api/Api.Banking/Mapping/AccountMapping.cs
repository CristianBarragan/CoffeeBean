using Graphgine.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Api.Banking.Mapping;

/// <summary>
/// Maps Domain.Model.Account onto Database.Entity.Account.
///
/// -----------------------------------------------------------------------
/// REVISED — see PORT-STATUS.md §1 for why this is smaller than it was
/// -----------------------------------------------------------------------
/// The first version of this file carried an explicit `Navigations` block
/// for Account.Contract and Account.Transaction. That turned out to be
/// unnecessary: `EntityNavigationConvention.Resolve` (in
/// Graphgine.SourceGenerators) walks a model's own C# properties, matches
/// each one against another mapped model by name/type, and finds the join
/// path by walking the FK graph that `EntityForeignKeyEmitterGenerator`
/// builds from Database.Entity.Banking's own EF Fluent
/// `HasOne/HasMany/WithOne/WithMany/HasForeignKey` calls
/// (AccountEntityConfiguration already declares both edges this model
/// needs: Account 1-1 Contract via Contract.AccountId, and Account 1-many
/// Transaction via Transaction.AccountId). An explicit `Navigations` entry
/// is only consulted as a fallback for names the convention pass didn't
/// already resolve — for Account, it never got that far, so the block was
/// dead weight. Same story for `Fields`: `FieldMapGeneration` auto-matches
/// same-named, type-compatible scalar properties, so only the primary key
/// override needs stating here.
///
/// What this file leaves entirely to convention/inference (confirmed by
/// reading, not by running):
///   - AccountKey/AccountNumber/AccountName scalar fields (name + type
///     already match between Account and DataEntity.Account)
///   - Account.Contract / Account.Transaction navigations (both resolved
///     from the entity FK graph, matched by property name against the
///     related model's mapping)
///   - UpsertKeys (synthesized from Entity/Key below, since AccountKey
///     has no AliasProperty — it's a genuine owned column, not a
///     navigation reference)
///
/// -----------------------------------------------------------------------
/// STILL UNVERIFIED — no .NET SDK / NuGet access when this was written
/// -----------------------------------------------------------------------
/// This is syntactically valid C# against the real
/// Graphgine.Mapping.MappingDefinition record types, and was written by
/// tracing Graphgine.SourceGenerators/Parsing/MappingClassParser.cs (1886
/// lines) and the convention passes it feeds
/// (EntityNavigationConvention.cs, FieldMapGeneration.cs) closely enough
/// to match their exact expected shapes. But there is still no working
/// example anywhere in this repository that has gone through a real
/// `dotnet build` against `Graphgine.SourceGenerators`. Run that build as
/// the first next step; treat MappingNodeGenerator's own diagnostics
/// (`CBM0xx` / `EntityGraphDebug`) as authoritative over anything
/// asserted in this comment.
/// </summary>
public sealed class AccountMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Account),
        Schema = nameof(DataEntity.Schema.Accounting),

        // Single-entity shorthand — MappingClassParser expands this into a
        // full Entities[] entry (IsPrimary = true) and synthesizes
        // PrimaryKey/UpsertKeys from it automatically.
        Entity = typeof(DataEntity.Account),
        Key = nameof(DataEntity.Account.AccountKey)

        // No Fields, no Navigations: AccountNumber/AccountName match by
        // name+type, and Contract/Transaction resolve via the entity FK
        // graph. See header comment above.
    };
}
