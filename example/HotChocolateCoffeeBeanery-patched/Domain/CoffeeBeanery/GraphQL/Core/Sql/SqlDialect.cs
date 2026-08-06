using System.Globalization;
using System.Text;

namespace CoffeeBeanery.GraphQL.Core.Sql;

public interface SqlDialect
{
    void AppendQuotedIdentifier(StringBuilder sb, string identifier);
    void AppendQualifiedTable(StringBuilder sb, string schema, string table);
    void AppendLiteral(StringBuilder sb, string rawValue);
    string LeftJoinKeyword { get; }
    string InnerJoinKeyword { get; }

    void AppendUpsertConflict(
        StringBuilder sb,
        string[] conflictColumns,
        IEnumerable<(string Column, bool IsConflictColumn)> allColumns);
}

public sealed class PostgresDialect : SqlDialect
{
    public void AppendQuotedIdentifier(StringBuilder sb, string identifier)
        => sb.Append('"').Append(identifier.Replace("\"", "\"\"")).Append('"');

    public void AppendQualifiedTable(StringBuilder sb, string schema, string table)
    {
        AppendQuotedIdentifier(sb, schema);
        sb.Append('.');
        AppendQuotedIdentifier(sb, table);
    }

    public void AppendLiteral(StringBuilder sb, string rawValue)
    {
        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            sb.Append(rawValue);
            return;
        }
        sb.Append('\'').Append(rawValue.Replace("'", "''")).Append('\'');
    }

    public string LeftJoinKeyword => "LEFT JOIN";
    public string InnerJoinKeyword => "JOIN";

    public void AppendUpsertConflict(
        StringBuilder sb,
        string[] conflictColumns,
        IEnumerable<(string Column, bool IsConflictColumn)> allColumns)
    {
        if (conflictColumns.Length == 0)
        {
            sb.Append(" ON CONFLICT DO NOTHING");
            return;
        }

        sb.Append(" ON CONFLICT (");
        for (var i = 0; i < conflictColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            AppendQuotedIdentifier(sb, conflictColumns[i]);
        }
        sb.Append(") DO UPDATE SET ");

        var first = true;
        foreach (var (column, isConflict) in allColumns)
        {
            if (isConflict) continue;
            if (!first) sb.Append(", ");
            first = false;
            AppendQuotedIdentifier(sb, column);
            sb.Append(" = EXCLUDED.");
            AppendQuotedIdentifier(sb, column);
        }

        if (first)
        {
            sb.Length -= " DO UPDATE SET ".Length;
            sb.Append(" DO NOTHING");
        }
    }
}
