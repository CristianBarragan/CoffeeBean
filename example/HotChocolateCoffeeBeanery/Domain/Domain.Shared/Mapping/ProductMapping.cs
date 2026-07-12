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
                    nameof(DataEntity.Contract.ContractKey),

                IsPrimary = true
            },

            new()
            {
                Entity = typeof(DataEntity.Transaction),

                ModelKey =
                    nameof(Product.TransactionKey),

                EntityKey =
                    nameof(DataEntity.Transaction.TransactionKey),

                IsPrimary = true
            },
            new()
            {
                Entity = typeof(DataEntity.CustomerBankingRelationship),

                ModelKey =
                    nameof(Product.CustomerBankingRelationshipKey),

                EntityKey =
                    nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey),

                IsPrimary = true
            }
        ],
        PrimaryKey = [new()
        {
            Entity = typeof(DataEntity.Account),
            ModelKey = nameof(Account.AccountKey),
            ColumnKey =
                nameof(DataEntity.Account.Id)
        },new()
        {
            Entity = typeof(DataEntity.Contract),
            ModelKey = nameof(Contract.ContractKey),
            ColumnKey =
                nameof(DataEntity.Contract.Id)
        },new()
        {
            Entity = typeof(DataEntity.Transaction),
            ModelKey = nameof(Transaction.TransactionKey),
            ColumnKey =
                nameof(DataEntity.Transaction.Id)
        },new()
        {
            Entity = typeof(DataEntity.CustomerBankingRelationship),
            ModelKey = nameof(CustomerBankingRelationship.CustomerBankingRelationshipKey),
            ColumnKey =
                nameof(DataEntity.CustomerBankingRelationship.Id)
        }],
        UpsertKeys =[new()
        {
            Entity = typeof(DataEntity.Account),

            Column =
                nameof(DataEntity.Account.AccountKey)
        },new()
        {
            Entity = typeof(DataEntity.Contract),

            Column =
                nameof(DataEntity.Contract.ContractKey)
        },new()
        {
            Entity = typeof(DataEntity.Transaction),

            Column =
                nameof(DataEntity.Transaction.TransactionKey)
        },new()
        {
            Entity = typeof(DataEntity.CustomerBankingRelationship),

            Column =
                nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
        }],

        Fields = [
            new()
            {
                Source = nameof(Product.ProductType),
                Entity = typeof(DataEntity.Contract),
                Destination = nameof(DataEntity.Contract.ContractType),
                EnumMapping = new EnumMappingDefinition<ProductType, DataEntity.ContractType>()
            }
        ]
    };
}