using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class AccountMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Account),

        Schema = nameof(DataEntity.Schema.Account),

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.Account),

                ModelKey =
                    nameof(Account.AccountKey),

                EntityKey =
                    nameof(DataEntity.Account.AccountKey),

                IsPrimary = true
            }
        ],
        PrimaryKey = [ new()
        {
            Entity = typeof(DataEntity.Account),

            ModelKey = nameof(Account.AccountKey),
            
            ColumnKey =
                nameof(DataEntity.Account.Id)
        }],
        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.Account),

                Column =
                    nameof(DataEntity.Account.AccountKey)
            }
        ]
    };
}