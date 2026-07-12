using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class TransactionMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Transaction),

        Schema = nameof(DataEntity.Schema.Lending),

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.Transaction),

                ModelKey =
                    nameof(Transaction.TransactionKey),

                EntityKey =
                    nameof(DataEntity.Transaction.TransactionKey),

                IsPrimary = true
            }
        ],
        PrimaryKey = [new()
        {
            Entity = typeof(DataEntity.Transaction),
            ModelKey = nameof(Transaction.TransactionKey),
            ColumnKey =
                nameof(DataEntity.Transaction.Id)
        }],
        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.Transaction),

                Column =
                    nameof(DataEntity.Transaction.TransactionKey)
            }
        ]
    };
}