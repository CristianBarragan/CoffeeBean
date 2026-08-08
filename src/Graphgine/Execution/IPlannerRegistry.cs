namespace Graphgine.Execution
{
    public interface IPlannerRegistry
    {
        void Build(
            ushort entityId,
            in SelectionIR selection,
            ref QueryPlanBuilder builder,
            bool isRoot);

        void BuildMutation(
            ushort entityId,
            in MutationIR mutation,
            ref MutationPlanBuilder builder);

        bool IsValidEntity(ushort entityId);

        string GetEntityName(ushort entityId);
    }
}