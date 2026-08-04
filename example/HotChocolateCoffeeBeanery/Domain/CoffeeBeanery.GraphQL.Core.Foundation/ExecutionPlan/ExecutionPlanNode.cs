using CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

namespace CoffeeBeanery.GraphQL.Core.Foundation.ExecutionPlan;

public abstract record ExecutionPlanNode;

public sealed record ScanNode(
    EntityMetadata Entity
) : ExecutionPlanNode;

public sealed record JoinNode(
    ExecutionPlanNode Left,
    ExecutionPlanNode Right,
    JoinCondition Condition
) : ExecutionPlanNode;

public sealed record MaterializeNode(
    Type ModelType,
    ExecutionPlanNode Source
) : ExecutionPlanNode;

public sealed record MutationNode(
    EntityMetadata Entity,
    IReadOnlyList<MutationColumn> Columns,
    IReadOnlyList<ExecutionPlanNode> Children
) : ExecutionPlanNode;

public sealed record GraphEdgeNode(
    GraphMetadata Graph,
    ExecutionPlanNode From,
    ExecutionPlanNode To
) : ExecutionPlanNode;

public sealed record ProviderBoundaryNode(
    IExecutionProvider Provider,
    ExecutionPlanNode Source
) : ExecutionPlanNode;
