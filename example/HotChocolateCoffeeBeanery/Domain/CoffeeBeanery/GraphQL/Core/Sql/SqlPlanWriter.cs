using System.Text;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace CoffeeBeanery.GraphQL.Core.Sql;

public sealed class SqlPlanWriter
{
    private readonly IEntityMetaProvider _meta;
    private readonly SqlDialect _dialect;

    public SqlPlanWriter(IEntityMetaProvider meta, SqlDialect dialect)
    {
        _meta = meta;
        _dialect = dialect;
    }

    private void AppendQualifiedTable(StringBuilder sb, ushort storageEntityId)
        => _dialect.AppendQualifiedTable(sb, _meta.EntitySchema[storageEntityId], _meta.EntityTable[storageEntityId]);

    private void AppendJoin(StringBuilder sb, in JoinSpec join, in QueryPlan plan)
    {
        var keyword = join.Kind == JoinKind.Left ? _dialect.LeftJoinKeyword : _dialect.InnerJoinKeyword;
        sb.Append(keyword).Append(' ');
        AppendQualifiedTable(sb, join.ChildStorageEntityId);
        sb.Append(' ');
        _dialect.AppendQuotedIdentifier(sb, join.ChildAlias);
        // ...
    }

    // AppendFieldValue delegates to _dialect.AppendLiteral instead of AppendQuotedValue
    // AppendDoUpdateSet/AppendDoUpdateSetFromNames delegate to _dialect.AppendUpsertConflict
}