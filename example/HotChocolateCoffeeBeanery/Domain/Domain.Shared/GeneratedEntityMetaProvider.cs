using CoffeeBeanery.GraphQL.Core.Runtime;

namespace Domain.Shared
{
    public sealed class GeneratedEntityMetaProvider : IEntityMetaProvider
    {
        public int Count => EntityId.Count;
        public string[] Table => EntityMeta.Table;
        public string[] Schema => EntityMeta.Schema;
        public string[][] ColumnName => EntityMeta.ColumnName;
        public string[][] FieldName => EntityMeta.FieldName;

        public bool TryGetEntityId(string modelName, out ushort entityId)
        {
            for (ushort i = 0; i < EntityId.Count; i++)
            {
                if (string.Equals(EntityMeta.Table[i], modelName,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    entityId = i;
                    return true;
                }
            }

            entityId = 0;
            return false;
        }
    }
}