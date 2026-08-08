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

                ModelKey = nameof(Transaction.TransactionKey),

                EntityKey = nameof(DataEntity.Transaction.TransactionKey),

                IsPrimary = true
            },

            new()
            {
                Entity = typeof(DataEntity.Account),

                ModelKey = nameof(Transaction.AccountKey),

                EntityKey = nameof(DataEntity.Account.AccountKey)
            },

            new()
            {
                Entity = typeof(DataEntity.Contract),

                ModelKey = nameof(Transaction.ContractKey),

                EntityKey = nameof(DataEntity.Contract.ContractKey)
            }
        ]
    };
}