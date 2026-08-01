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
        ],
        //
        // ForeignKeys =
        // [
        //     new()
        //     {
        //         Entity = typeof(DataEntity.Transaction),
        //         Column = nameof(DataEntity.Transaction.AccountId),
        //         PrincipalEntity = typeof(DataEntity.Account),
        //         PrincipalColumn = nameof(DataEntity.Account.Id)
        //     },
        //
        //     new()
        //     {
        //         Entity = typeof(DataEntity.Transaction),
        //         Column = nameof(DataEntity.Transaction.ContractId),
        //         PrincipalEntity = typeof(DataEntity.Contract),
        //         PrincipalColumn = nameof(DataEntity.Contract.Id)
        //     }
        // ],

        PrimaryKey =
        [
            new()
            {
                Entity = typeof(DataEntity.Transaction),
                // ModelKey = nameof(Transaction.TransactionKey),
                ColumnKey = nameof(DataEntity.Transaction.Id)
            }
        ],

        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.Transaction),
                Column = nameof(DataEntity.Transaction.TransactionKey)
            }
        ],

        // Fields =
        // [
        //     new()
        //     {
        //         Source = nameof(Transaction.AccountKey),
        //         Entity = typeof(DataEntity.Account),
        //         Destination = nameof(DataEntity.Account.AccountKey)
        //     },
        //
        //     new()
        //     {
        //         Source = nameof(Transaction.ContractKey),
        //         Entity = typeof(DataEntity.Contract),
        //         Destination = nameof(DataEntity.Contract.ContractKey)
        //     }
        // ]
    };
}