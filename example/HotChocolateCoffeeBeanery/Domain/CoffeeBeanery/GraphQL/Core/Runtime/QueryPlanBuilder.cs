using System.Collections.Immutable;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public ref struct QueryPlanBuilder
{
    private ushort _rootEntityId;
    private ushort _rootStorageEntityId;
    private string? _rootAlias;

    private InlineArray64<ColumnSpec> _columns;
    private int _columnCount;

    private InlineArray32<JoinSpec> _joins;
    private int _joinCount;

    private InlineArray32<GraphJoinSpec> _graphJoins;
    private int _graphJoinCount;

    private InlineArray32<GraphResultJoinSpec> _graphResultJoins;
    private int _graphResultJoinCount;

    private readonly HashSet<JoinKey> _joinKeys = new();
    private readonly HashSet<GraphJoinKey> _graphJoinKeys;


    public QueryPlanBuilder()
    {
        _columns = default;
        _joins = default;
        _graphJoins = default;
        _graphResultJoins = default;

        _columnCount = 0;
        _joinCount = 0;
        _graphJoinCount = 0;
        _graphResultJoinCount = 0;

        _rootEntityId = 0;
        _rootStorageEntityId = 0;
        _rootAlias = null;

        _joinKeys = new();
        _graphJoinKeys = new();
    }


    public void BeginCompositeChain(
        ushort entityId,
        ushort storageEntityId,
        string outputAlias)
    {
        _rootEntityId = entityId;
        _rootStorageEntityId = storageEntityId;
        _rootAlias = outputAlias;
    }


// compatibility overload
// NOTE: storage id cannot be inferred safely.
// Keep only for old generated code.
// New generators should always call the 3 argument overload.
    public void BeginCompositeChain(
        ushort entityId,
        string outputAlias)
    {
        _rootEntityId = entityId;
        _rootAlias = outputAlias;

        if (_rootStorageEntityId == 0)
        {
            throw new InvalidOperationException(
                "BeginCompositeChain(entityId, outputAlias) is obsolete. " +
                "Generated planners must provide storageEntityId.");
        }
    }


    public void SetRoot(
        ushort entityId,
        ushort storageEntityId,
        string outputAlias)
    {
        _rootEntityId = entityId;
        _rootStorageEntityId = storageEntityId;
        _rootAlias = outputAlias;
    }


    public void AddColumn(
        ushort entityId,
        ushort storageEntityId,
        ushort columnId,
        string entityOutputAlias,
        string columnOutputAlias)
    {
        if (_columnCount >= 64)
        {
            throw new InvalidOperationException(
                "Maximum query column count exceeded.");
        }

        _columns[_columnCount++] =
            new ColumnSpec(
                entityId,
                storageEntityId,
                columnId,
                entityOutputAlias,
                columnOutputAlias);
    }
    
    public void AddJoin(
        ushort fromEntityId,
        ushort fromStorageEntityId,
        ushort toEntityId,
        ushort toStorageEntityId,
        ushort fromColumnId,
        ushort toColumnId,
        JoinKind kind,
        string toOutputAlias)
    {
        var key = new JoinKey(
            fromEntityId,
            fromStorageEntityId,
            fromColumnId,
            toEntityId,
            toStorageEntityId,
            toColumnId);

        if (!_joinKeys.Add(key))
            return;


        _joins[_joinCount++] =
            new JoinSpec(
                fromEntityId,
                fromStorageEntityId,
                toEntityId,
                toStorageEntityId,
                fromColumnId,
                toColumnId,
                kind,
                toOutputAlias);
    }


    public void AddGraphJoin(
        ushort entityId,
        ushort storageEntityId,
        string graphName,
        string edgeLabel,
        string edgeKeyColumn,
        string fromLabel,
        string fromGraphProperty,
        string fromAlias,
        string fromJoinColumn,
        string toLabel,
        string toGraphProperty,
        string toAlias,
        string toJoinColumn,
        string joinAlias)
    {
        _graphJoins[_graphJoinCount++] =
            new GraphJoinSpec(
                entityId,
                storageEntityId,
                graphName,
                edgeLabel,
                edgeKeyColumn,
                fromLabel,
                fromGraphProperty,
                fromAlias,
                fromJoinColumn,
                toLabel,
                toGraphProperty,
                toAlias,
                toJoinColumn,
                joinAlias);
    }


    public void AddGraphResultJoin(
        string fromAlias,
        string fromColumnName,
        ushort toEntityId,
        ushort toStorageEntityId,
        ushort toColumnId,
        JoinKind kind,
        string toOutputAlias)
    {
        _graphResultJoins[_graphResultJoinCount++] =
            new GraphResultJoinSpec(
                fromAlias,
                fromColumnName,
                toEntityId,
                toStorageEntityId,
                toColumnId,
                kind,
                toOutputAlias);
    }


    public QueryPlan Build()
    {
        var columns =
            ImmutableArray.CreateBuilder<ColumnSpec>(_columnCount);

        for (int i = 0; i < _columnCount; i++)
            columns.Add(_columns[i]);


        var joins =
            ImmutableArray.CreateBuilder<JoinSpec>(_joinCount);

        for (int i = 0; i < _joinCount; i++)
            joins.Add(_joins[i]);


        var graphJoins =
            ImmutableArray.CreateBuilder<GraphJoinSpec>(_graphJoinCount);

        for (int i = 0; i < _graphJoinCount; i++)
            graphJoins.Add(_graphJoins[i]);


        var graphResultJoins =
            ImmutableArray.CreateBuilder<GraphResultJoinSpec>(_graphResultJoinCount);

        for (int i = 0; i < _graphResultJoinCount; i++)
            graphResultJoins.Add(_graphResultJoins[i]);


        return new QueryPlan(
            _rootEntityId,
            _rootStorageEntityId,
            _rootAlias ?? string.Empty,
            columns.ToImmutable(),
            joins.ToImmutable(),
            graphJoins.ToImmutable(),
            graphResultJoins.ToImmutable());
    }
}