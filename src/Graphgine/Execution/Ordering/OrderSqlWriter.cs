using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using Foundgine;
using Graphgine.Execution;
using Graphgine.Execution.Filtering;

namespace Graphgine.Execution.Ordering;

public sealed record ResolvedOrderTerm(
    string Alias,
    string ColumnName,
    ushort StorageEntityId,
    ushort ColumnId,
    SortDirection Direction);

/// <summary>
/// Resolves OrderCompiler's parsed terms into real (alias, column)
/// references for a specific QueryPlan, and builds ORDER BY / compound
/// keyset cursor SQL from them.
///
/// KNOWN LIMITATION: a navigation term (e.g. `customer.firstNaming`) can
/// only be resolved if that navigation was actually joined into the query
/// already -- i.e. the client also selected `customer { ... }` as part of
/// the query fields. Ordering by a navigation field without selecting it
/// throws via QueryPlanAliasResolver rather than silently forcing an extra
/// join; forcing an unselected join in just for ordering is a reasonable
/// follow-up (mirrors PagingSqlWriter.EnsurePrimaryKeySelected's approach)
/// but is real additional work, not built here.
///
/// COMPOUND CURSOR SCOPE: when ordering is combined with pagination, the
/// cursor must encode every sort term's value (not just the primary key) --
/// otherwise two rows with the same sort-field value would be
/// indistinguishable to the seek predicate. This only works correctly when
/// every term (the user's sort terms AND the primary-key tiebreaker) shares
/// one direction: Postgres row-comparison `(a, b) > (x, y)` compares every
/// component with the same operator, so a genuinely mixed-direction sort
/// (`fieldA ASC, fieldB DESC`) can't be expressed as one row comparison --
/// it would need a per-column disjunctive WHERE clause instead. That's not
/// built here; BuildSeekPredicate throws NotSupportedException for mixed
/// directions rather than silently paginating wrong. Single-direction sorts
/// (the overwhelmingly common case -- "newest first", "by name") work
/// correctly, including DESC: the primary-key tiebreaker adopts the same
/// direction as the user's terms rather than being hardcoded ASC, which is
/// what makes the row-comparison trick valid for a DESC sort too.
/// </summary>
public static class OrderSqlWriter
{
    /// <summary>
    /// Phase 1: resolves each term's navigation path down to a real field,
    /// using only the entity graph -- no QueryPlan needed yet. Callers use
    /// this to force each term's column into the query's projection
    /// (mirrors PagingSqlWriter.EnsurePrimaryKeySelected) BEFORE
    /// translating to a QueryPlan, then call ResolveAliases afterward with
    /// the translated plan to get real SQL aliases. Split into two phases
    /// specifically because alias resolution needs the QueryPlan's actual
    /// Joins, which don't exist until after translation -- but forcing a
    /// column into the projection has to happen before translation.
    /// </summary>
    public static List<(RuntimeFieldMetadata Field, SortDirection Direction)> ResolveFields(
        List<OrderTerm> terms,
        ImmutableArray<RuntimeEntityMetadata> entityGraph,
        ushort rootModelEntityId)
    {
        var resolved =
            new List<(RuntimeFieldMetadata, SortDirection)>();

        foreach (var term in terms)
        {
            var currentEntityId =
                rootModelEntityId;

            RuntimeFieldMetadata? field =
                null;

            for (var i = 0; i < term.Path.Count; i++)
            {
                var segment =
                    term.Path[i];

                var entity =
                    entityGraph.FirstOrDefault(e => e.EntityId == currentEntityId)
                    ?? throw new InvalidOperationException(
                        $"Entity id {currentEntityId} not found while " +
                        $"resolving order path '{string.Join(".", term.Path)}'.");

                var isLast =
                    i == term.Path.Count - 1;

                if (isLast)
                {
                    field =
                        entity.Fields.Values.FirstOrDefault(f =>
                            string.Equals(f.Name, segment, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException(
                            $"Unknown field '{segment}' while resolving " +
                            $"order path '{string.Join(".", term.Path)}'.");
                }
                else
                {
                    var nav =
                        entity.Navigations.FirstOrDefault(n =>
                            string.Equals(n.NavigationName, segment, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException(
                            $"Unknown navigation '{segment}' while " +
                            $"resolving order path '{string.Join(".", term.Path)}'. " +
                            "(A navigation can only be ordered by if it was " +
                            "also selected in the query.)");

                    currentEntityId = nav.TargetEntityId;
                }
            }

            resolved.Add((field!, term.Direction));
        }

        return resolved;
    }

    /// <summary>
    /// Phase 2: resolves each already-field-resolved term's real SQL alias
    /// against a translated QueryPlan. Call after ensuring every term's
    /// column (via ResolveFields) was forced into the projection and the
    /// QueryNode translated -- otherwise QueryPlanAliasResolver will
    /// correctly throw "not part of this query's plan" for any navigation
    /// that wasn't actually joined in.
    /// </summary>
    public static List<ResolvedOrderTerm> ResolveAliases(
        List<(RuntimeFieldMetadata Field, SortDirection Direction)> fieldTerms,
        in QueryPlan plan)
    {
        var resolved =
            new List<ResolvedOrderTerm>();

        foreach (var (field, direction) in fieldTerms)
        {
            var alias =
                QueryPlanAliasResolver.ResolveAlias(
                    plan,
                    field.StorageEntityId);

            var columnName =
                ResolveColumnName(
                    field.StorageEntityId,
                    field.ColumnId);

            resolved.Add(
                new ResolvedOrderTerm(
                    alias,
                    columnName,
                    field.StorageEntityId,
                    field.ColumnId,
                    direction));
        }

        return resolved;
    }

    /// <summary>
    /// Builds "ORDER BY term1 ASC, term2 DESC, ..., pkAlias.pkColumn dir" --
    /// the primary key tiebreaker always comes last, in the SAME direction
    /// as the (single, required-uniform -- see class remarks) user terms,
    /// or ASC if there are no user terms at all.
    /// </summary>
    public static string BuildOrderByClause(
        IReadOnlyList<ResolvedOrderTerm> terms,
        string primaryKeyAlias,
        string primaryKeyColumnName)
    {
        var tiebreakerDirection =
            terms.Count > 0 ? terms[0].Direction : SortDirection.Asc;

        var parts =
            new List<string>();

        foreach (var term in terms)
        {
            parts.Add(
                $"\"{term.Alias}\".\"{term.ColumnName}\" " +
                (term.Direction == SortDirection.Desc ? "DESC" : "ASC"));
        }

        parts.Add(
            $"\"{primaryKeyAlias}\".\"{primaryKeyColumnName}\" " +
            (tiebreakerDirection == SortDirection.Desc ? "DESC" : "ASC"));

        return "ORDER BY " + string.Join(", ", parts);
    }

    /// <summary>
    /// True only when every term shares one direction -- the precondition
    /// for BuildSeekPredicate's single row-comparison to be correct. Zero
    /// or one term is trivially uniform.
    /// </summary>
    public static bool IsUniformDirection(IReadOnlyList<ResolvedOrderTerm> terms) =>
        terms.Count <= 1 ||
        terms.All(t => t.Direction == terms[0].Direction);

    public static string EncodeCompoundCursor(IReadOnlyList<object?> values) =>
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(
                    values.Select(v => v?.ToString()).ToArray())));

    public static string[] DecodeCompoundCursor(string cursor) =>
        JsonSerializer.Deserialize<string[]>(
            Encoding.UTF8.GetString(
                Convert.FromBase64String(cursor)))
        ?? Array.Empty<string>();

    /// <summary>
    /// Builds the "(term1, term2, ..., pk) > (@p0, @p1, ..., @pN)" (or
    /// "&lt;" for an all-DESC sort) seek predicate for an `after` cursor.
    /// Every component is compared ::text, same reasoning and same known
    /// limitation as PagingSqlWriter's single-key predicate: no column
    /// type information is available here to bind a natively-typed
    /// parameter, so a text comparison is what's guaranteed correct
    /// regardless of the underlying column type, at the cost of bypassing
    /// a native index on it.
    /// </summary>
    public static string? BuildSeekPredicate(
        IReadOnlyList<ResolvedOrderTerm> terms,
        string primaryKeyAlias,
        string primaryKeyColumnName,
        string? afterCursor,
        FilterCompilationContext context)
    {
        if (string.IsNullOrEmpty(afterCursor))
            return null;

        if (!IsUniformDirection(terms))
        {
            throw new NotSupportedException(
                "Pagination combined with a mixed-direction sort (some " +
                "terms ASC, others DESC) is not supported -- see " +
                "OrderSqlWriter remarks.");
        }

        var direction =
            terms.Count > 0 ? terms[0].Direction : SortDirection.Asc;

        var decoded =
            DecodeCompoundCursor(afterCursor);

        var columnRefs =
            terms
                .Select(t => $"\"{t.Alias}\".\"{t.ColumnName}\"::text")
                .Append($"\"{primaryKeyAlias}\".\"{primaryKeyColumnName}\"::text")
                .ToList();

        var paramRefs =
            new List<string>();

        for (var i = 0; i < columnRefs.Count; i++)
        {
            var value =
                i < decoded.Length ? decoded[i] : null;

            var param =
                context.AddParameter(value);

            paramRefs.Add("@" + param);
        }

        var op =
            direction == SortDirection.Desc ? "<" : ">";

        return
            $"({string.Join(", ", columnRefs)}) {op} ({string.Join(", ", paramRefs)})";
    }

    private static string ResolveColumnName(
        ushort storageEntityId,
        ushort columnId)
    {
        var entity =
            GeneratedMetadata.GetEntity(storageEntityId);

        foreach (var column in entity.Columns)
        {
            if (column.Id.Value == columnId)
                return column.Name;
        }

        throw new InvalidOperationException(
            $"Column id {columnId} not found on entity '{entity.Name}'.");
    }
}
