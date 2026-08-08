using System;
using System.Collections.Generic;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Foundation;
using CoffeeBeanery.GraphQL.Core.Foundation.Metadata;
using CoffeeBeanery.GraphQL.Core.Foundation.QueryPlan;
using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Paging;

/// <summary>
/// Keyset (stable) cursor pagination, chosen over offset-based pagination
/// specifically because offset drifts (skips/duplicates rows) under
/// concurrent inserts/deletes -- a real correctness concern for a
/// paginated listing over live banking data, not just a UX nuance.
///
/// Orders and seeks by ModelMetadata.PrimaryKey rather than requiring
/// general ORDER BY support first -- this is an internal implementation
/// detail for stable pagination, not user-facing sorting. A model with no
/// PrimaryKey (see IdEmitter.EmitModelMetadata remarks) cannot be paginated
/// this way; callers must handle that explicitly.
///
/// KNOWN LIMITATION: the cursor predicate compares the primary key column
/// cast to ::text (`"alias"."col"::text > @cursor`), not its native type --
/// Foundation.Metadata doesn't currently carry the PK column's CLR/SQL
/// type, so this is the only comparison guaranteed correct regardless of
/// whether the key is text, integer, or uuid. It works correctly, but
/// bypasses any native index on that column in favor of a text comparison.
/// Revisit once column type is available, for a numeric/uuid-typed
/// comparison that can use the index directly.
/// </summary>
public static class PagingSqlWriter
{
    public static string EncodeCursor(object key) =>
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                key.ToString() ?? string.Empty));

    public static string DecodeCursor(string cursor) =>
        Encoding.UTF8.GetString(
            Convert.FromBase64String(cursor));

    public static string ResolvePrimaryKeyColumnName(ColumnReference primaryKey)
    {
        foreach (var column in primaryKey.Entity.Columns)
        {
            if (column.Id.Value == primaryKey.ColumnId)
                return column.Name;
        }

        throw new InvalidOperationException(
            $"Primary key column id {primaryKey.ColumnId} not found on " +
            $"entity '{primaryKey.Entity.Name}'.");
    }

    /// <summary>
    /// Ensures the model's primary key column is present in the query's
    /// projection, inserting a FieldBinding for it if the client didn't
    /// select that field themselves -- a cursor can't be computed for a
    /// row whose key value was never fetched. Safe to always do: adding
    /// an already-declared column to a ProjectionNode.Fields list that
    /// wasn't otherwise selected just fills in a previously-absent slot
    /// in QueryPlan.BuildColumnMap's per-entity map (sized to the
    /// entity's full column count, not just what was selected) -- it
    /// does not disturb any other field's mapping.
    /// </summary>
    public static QueryNode EnsurePrimaryKeySelected(
        QueryNode root,
        ColumnReference primaryKey)
    {
        if (root is not MaterializeNode materialize)
        {
            throw new ArgumentException(
                "Expected the root QueryNode to be a MaterializeNode.",
                nameof(root));
        }

        if (materialize.Source is ProjectionNode projection)
        {
            var alreadySelected =
                false;

            foreach (var field in projection.Fields)
            {
                if (field.Source.Entity.EntityId.Value == primaryKey.Entity.EntityId.Value &&
                    field.Source.ColumnId == primaryKey.ColumnId)
                {
                    alreadySelected = true;
                    break;
                }
            }

            if (alreadySelected)
                return root;

            var fields =
                new List<FieldBinding>(projection.Fields)
                {
                    // FieldId 0 as a placeholder -- this binding exists only
                    // to force the column into the SELECT list for cursor
                    // purposes, not to be resolved back to a real model
                    // field (matches this codebase's existing convention of
                    // using FieldId 0 for fields with no real field id).
                    new(primaryKey, 0),
                };

            return
                materialize with
                {
                    Source = projection with { Fields = fields },
                };
        }

        return
            materialize with
            {
                Source = new ProjectionNode(
                    materialize.Source,
                    new List<FieldBinding> { new(primaryKey, 0) }),
            };
    }
    /// <summary>
    /// Deterministic ORDER BY for keyset pagination -- ascending by the
    /// primary key column, matching BuildAfterPredicate's `> @cursor` seek
    /// direction below (forward-only pagination; see the class remarks on
    /// backward pagination not being implemented yet).
    /// </summary>
    public static string BuildOrderBy(
        string rootAlias,
        string primaryKeyColumnName) =>
        $"ORDER BY \"{rootAlias}\".\"{primaryKeyColumnName}\" ASC";

    /// <summary>
    /// Returns the `> @pN` predicate for an `after` cursor, and adds the
    /// bound parameter to context -- or null if there's no cursor to seek
    /// past. Caller combines this (via AND) with any filter WHERE clause.
    /// </summary>
    public static string? BuildAfterPredicate(
        string rootAlias,
        string primaryKeyColumnName,
        string? afterCursor,
        FilterCompilationContext context)
    {
        if (string.IsNullOrEmpty(afterCursor))
            return null;

        var decoded =
            DecodeCursor(afterCursor);

        var param =
            context.AddParameter(decoded);

        return
            $"\"{rootAlias}\".\"{primaryKeyColumnName}\"::text > @{param}";
    }
}
