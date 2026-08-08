using System.Collections.Generic;
using Foundgine;
using Foundgine.Metadata;

namespace Graphgine.Execution.Filtering;

/// <summary>
/// Builds the RuntimeEntityMetadata FilterMetadataResolver needs, from
/// data GeneratedMetadata.{Model} (IdEmitter's output) already carries --
/// no new generator emission needed for field-level filtering.
///
/// SCOPE: only the model's OWN fields are exposed (Navigations is always
/// empty). Two things are deliberately not built here:
///
/// - Navigation filters (`customer: { firstName: { eq: "Bob" } }`) and
///   collection filters (`some`/`all`/`none`) -- there is currently no
///   runtime-queryable navigation-name -> target-EntityId map anywhere in
///   the project (EntityNavigationConvention only runs at generator time).
///   FilterMetadataResolver.ResolveNavigation already fails loudly with a
///   clear "Unknown navigation ..." error when Navigations is empty, which
///   is the correct behavior here -- a clear error, not silently wrong SQL.
/// - Fields whose FieldMetadata.Column is null (computed/derived fields
///   with no direct column, e.g. enum-mapped fields) are skipped -- there
///   is no column to filter on.
///
/// Whatever consumes this must also only accept filters on the query's
/// ROOT storage entity for the same reason FilterSqlWriter enforces it:
/// resolving which SQL alias a non-root (composite-secondary or joined)
/// entity's column belongs to isn't handled yet either.
/// </summary>
public static class RuntimeEntityMetadataRegistry
{
    public static RuntimeEntityMetadata GetRootOnly(ushort modelEntityId, IMetadataProvider metadata)
    {
        var model = metadata.GetModel(modelEntityId);

        var fields = new Dictionary<ushort, RuntimeFieldMetadata>();

        foreach (var field in model.Fields)
        {
            if (field.Column is null)
                continue;

            fields[field.Id.Value] =
                new RuntimeFieldMetadata(
                    field.Id.Value,
                    field.Name,
                    field.Column.ColumnId,
                    field.Column.Entity.EntityId.Value);
        }

        return new RuntimeEntityMetadata(
            modelEntityId,
            model.Name,
            fields,
            new List<RuntimeNavigationMetadata>());
    }

    /// <summary>
    /// Back-compat overload -- forwards to the same generated singleton
    /// the static GeneratedMetadata class itself would have used. New
    /// callers should prefer the IMetadataProvider overload above.
    /// </summary>
    public static RuntimeEntityMetadata GetRootOnly(ushort modelEntityId)
        => GetRootOnly(modelEntityId, GeneratedMetadataProvider.Instance);
}
