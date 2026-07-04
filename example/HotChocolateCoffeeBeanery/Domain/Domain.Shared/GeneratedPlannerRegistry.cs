using CoffeeBeanery.GraphQL.Core.Runtime;

namespace Domain.Shared
{
    public sealed class GeneratedPlannerRegistry : IPlannerRegistry
    {
        public void Build(ushort entityId, in SelectionIR selection, ref QueryPlanBuilder builder)
            => PlannerRegistry.Build(entityId, selection, ref builder);

        public void BuildMutation(ushort entityId, in MutationIR mutation, ref MutationPlanBuilder builder)
            => PlannerRegistry.BuildMutation(entityId, mutation, ref builder);

        public bool IsValidEntity(ushort entityId)
            => PlannerRegistry.IsValidEntity(entityId);

        public string GetEntityName(ushort entityId)
            => PlannerRegistry.GetEntityName(entityId);
    }
}