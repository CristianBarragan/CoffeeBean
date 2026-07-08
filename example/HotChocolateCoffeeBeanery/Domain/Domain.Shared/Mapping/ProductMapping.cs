using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class ProductMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Product),

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.Account),

                ModelKey =
                    nameof(Product.AccountKey),

                EntityKey =
                    nameof(DataEntity.Account.AccountKey),

                IsPrimary = true
            },

            new()
            {
                Entity = typeof(DataEntity.Contract),

                ModelKey =
                    nameof(Product.ContractKey),

                EntityKey =
                    nameof(DataEntity.Contract.ContractKey)
            },

            new()
            {
                Entity = typeof(DataEntity.Transaction),

                ModelKey =
                    nameof(Product.TransactionKey),

                EntityKey =
                    nameof(DataEntity.Transaction.TransactionKey)
            },

            new()
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),

                ModelKey =
                    nameof(Product.CustomerBankingRelationshipKey),

                EntityKey =
                    nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            }
        ]
    };
}