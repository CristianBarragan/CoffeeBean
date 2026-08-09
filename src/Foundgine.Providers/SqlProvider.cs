using System.Runtime.CompilerServices;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Microsoft.Data.Sqlite;

using ExecutionContext =
    Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Providers;

/// <summary>
/// SQL execution provider.
///
/// Compiles a ProviderPlan into SQL through SqlTextTranslator, executes
/// that SQL against SQLite, and streams the result rows back as
/// ExecutionRow instances.
///
/// SQLite is intentionally the first backend so the initial end-to-end
/// execution path can use a real relational database without requiring an
/// external database server.
///
/// The important result-mapping rule is that EntityId alone is NOT enough
/// to identify a result occurrence. A self-join can contain:
///
///     Employee #0
///     Employee #1
///     Employee #2
///
/// Therefore every result occurrence is keyed by both EntityId and
/// OccurrenceIndex.
/// </summary>
public sealed class SqlExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Sql;

    public async IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        var connectionString =
            ResolveConnectionString(context);

        var translation =
            SqlTextTranslator.Translate(plan);

        await using var connection =
            new SqliteConnection(connectionString);

        await connection
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            translation.CommandText;

        foreach (var parameter in translation.Parameters)
        {
            command.Parameters.AddWithValue(
                parameter.Name,
                parameter.Value ?? DBNull.Value);
        }

        await using var reader =
            await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);

        while (await reader
                   .ReadAsync(cancellationToken)
                   .ConfigureAwait(false))
        {
            yield return ReadRow(
                reader,
                translation.Columns);
        }
    }

    private static string ResolveConnectionString(
        ExecutionContext context)
    {
        if (!context.Variables.TryGetValue(
                "ConnectionString",
                out var value) ||
            value is not string connectionString ||
            string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{nameof(SqlExecutionProvider)} requires a non-empty " +
                $"\"ConnectionString\" entry in " +
                $"{nameof(ExecutionContext)}." +
                $"{nameof(ExecutionContext.Variables)}.");
        }

        return connectionString;
    }

    /// <summary>
    /// Converts the current ADO.NET row into Foundgine execution
    /// occurrences.
    ///
    /// The SQL translator has already assigned every selected column to
    /// an occurrence:
    ///
    ///     t0 -> occurrence 0
    ///     t1 -> occurrence 1
    ///     t2 -> occurrence 2
    ///
    /// This method preserves that identity instead of collapsing repeated
    /// entities into a Dictionary keyed only by EntityId.
    /// </summary>
    private static ExecutionRow ReadRow(
        SqliteDataReader reader,
        IReadOnlyList<SqlColumnMap> columns)
    {
        var valuesByOccurrence =
            new Dictionary<
                (EntityId EntityId, int OccurrenceIndex),
                object?[]>();

        var sizeByOccurrence =
            new Dictionary<
                (EntityId EntityId, int OccurrenceIndex),
                int>();

        foreach (var map in columns)
        {
            var key =
                (map.Entity.EntityId, map.OccurrenceIndex);

            if (!valuesByOccurrence.TryGetValue(
                    key,
                    out var values))
            {
                values =
                    new object?[map.Entity.Columns.Count];

                valuesByOccurrence[key] =
                    values;

                sizeByOccurrence[key] =
                    map.Entity.Columns.Count;
            }
        }

        for (var ordinal = 0;
             ordinal < columns.Count;
             ordinal++)
        {
            var map =
                columns[ordinal];

            var key =
                (map.Entity.EntityId, map.OccurrenceIndex);

            var values =
                valuesByOccurrence[key];

            var columnIndex =
                FindColumnIndex(
                    map.Entity,
                    map.ColumnId);

            values[columnIndex] =
                reader.IsDBNull(ordinal)
                    ? null
                    : reader.GetValue(ordinal);
        }

        var occurrences =
            valuesByOccurrence
                .OrderBy(x => x.Key.OccurrenceIndex)
                .Select(
                    x => new EntityOccurrence(
                        x.Key.EntityId,
                        x.Key.OccurrenceIndex,
                        x.Value))
                .ToArray();

        return new ExecutionRow(occurrences);
    }

    private static int FindColumnIndex(
        EntityMetadata entity,
        ushort columnId)
    {
        for (var index = 0;
             index < entity.Columns.Count;
             index++)
        {
            if (entity.Columns[index].Id.Value == columnId)
                return index;
        }

        throw new InvalidOperationException(
            $"Entity '{entity.Name}' has no column with id {columnId}.");
    }
}