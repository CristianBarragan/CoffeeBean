using System.Runtime.CompilerServices;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Microsoft.Data.Sqlite;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Providers;

/// <summary>
/// SQL execution provider. Compiles a <see cref="ProviderPlan"/> to a SQL
/// statement via <see cref="SqlTextTranslator"/>, runs it against a SQLite
/// database, and streams the results back as <see cref="ExecutionRow"/>.
///
/// SQLite is the first backend on purpose (item 7 of the architecture
/// review: "keep the provider abstraction, implement only one provider
/// initially") — it needs no external server, so the first Banking E2E can
/// run against a real database with zero setup. Swapping in Npgsql or
/// SqlClient later only touches this file and <see cref="SqlTextTranslator"/>'s
/// identifier-quoting assumption, not Foundgine.Builders, Foundgine.Planning,
/// or Foundgine.Execution.Contracts.
/// </summary>
public sealed class SqlExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Sql;

    public async IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        var connectionString = ResolveConnectionString(context);
        var translation = SqlTextTranslator.Translate(plan);
        var layout = BuildEntityLayout(translation.Columns);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = translation.CommandText;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return ReadRow(reader, translation.Columns, layout);
        }
    }

    private static string ResolveConnectionString(ExecutionContext context)
    {
        if (!context.Variables.TryGetValue("ConnectionString", out var value) ||
            value is not string connectionString ||
            string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{nameof(SqlExecutionProvider)} requires a non-empty \"ConnectionString\" entry " +
                $"in {nameof(ExecutionContext)}.{nameof(ExecutionContext.Variables)}.");
        }

        return connectionString;
    }

    private static ExecutionRow ReadRow(
        SqliteDataReader reader,
        IReadOnlyList<SqlColumnMap> columns,
        IReadOnlyDictionary<EntityId, EntityLayout> layout)
    {
        var entities = new Dictionary<ushort, object?[]>();

        for (var ordinal = 0; ordinal < columns.Count; ordinal++)
        {
            var map = columns[ordinal];
            var entityId = map.Entity.EntityId;
            var entityLayout = layout[entityId];

            if (!entities.TryGetValue(entityId.Value, out var values))
            {
                values = new object?[entityLayout.Size];
                entities[entityId.Value] = values;
            }

            values[entityLayout.ColumnIndex[map.ColumnId]] =
                reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
        }

        return new ExecutionRow(entities);
    }

    private readonly record struct EntityLayout(int Size, IReadOnlyDictionary<ushort, int> ColumnIndex);

    private static Dictionary<EntityId, EntityLayout> BuildEntityLayout(IReadOnlyList<SqlColumnMap> columns)
    {
        var layout = new Dictionary<EntityId, EntityLayout>();

        foreach (var map in columns)
        {
            if (layout.ContainsKey(map.Entity.EntityId))
                continue;

            var index = new Dictionary<ushort, int>();
            for (var i = 0; i < map.Entity.Columns.Count; i++)
                index[map.Entity.Columns[i].Id.Value] = i;

            layout[map.Entity.EntityId] = new EntityLayout(map.Entity.Columns.Count, index);
        }

        return layout;
    }
}
