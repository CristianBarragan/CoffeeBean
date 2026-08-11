using Foundgine.Abstractions;
using System.Data.Common;
using Foundgine.Execution;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.Sql;

/// <summary>
/// Executes an already-compiled SQL plan against an ADO.NET connection.
/// Compilation and execution remain separate responsibilities.
/// </summary>
public sealed class SqlExecutionProvider : IExecutionProvider
{
    private readonly DbConnection _connection;

    public SqlExecutionProvider(DbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan is not SqlPlan sqlPlan)
            throw new ArgumentException("The SQL provider requires a SqlPlan.", nameof(plan));

        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = sqlPlan.CommandText;
        foreach (var binding in sqlPlan.EffectiveParameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + binding.Name;
            parameter.Value = binding.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<ExecutionRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            var cells = new Dictionary<ExecutionCellKey, object?>();

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                values[name] = value;

                var binding = sqlPlan.Columns.FirstOrDefault(x =>
                    string.Equals(x.ResultName, name, StringComparison.Ordinal));

                if (binding is not null)
                {
                    cells[new ExecutionCellKey(
                        binding.NodeId,
                        binding.EntityId,
                        binding.FieldId)] = value;
                }
            }

            rows.Add(new ExecutionRow(values, cells));
        }

        ExecutionPageInfo? pageInfo = null;
        if (sqlPlan.Pagination is { } paging)
        {
            var hasNext = rows.Count > paging.First;
            if (hasNext) rows.RemoveAt(rows.Count - 1);
            string? start = null;
            string? end = null;
            if (rows.Count > 0)
            {
                var firstValues = paging.CursorValues
                .Select(binding => rows[0].Values[binding.ResultName])
                .ToArray();
            var lastValues = paging.CursorValues
                .Select(binding => rows[^1].Values[binding.ResultName])
                .ToArray();

            if (firstValues.Any(value => value is null) || lastValues.Any(value => value is null))
                throw new InvalidOperationException("Cursor ordering fields cannot contain null values.");

            start = Query.CursorCodec.Encode(firstValues);
            end = Query.CursorCodec.Encode(lastValues);
            }
            pageInfo = new ExecutionPageInfo(start, end, hasNext, paging.After is not null);
        }

        return new ExecutionResult(rows, pageInfo);
    }
}
