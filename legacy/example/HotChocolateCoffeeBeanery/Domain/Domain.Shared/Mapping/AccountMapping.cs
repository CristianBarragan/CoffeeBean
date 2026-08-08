using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class AccountMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(Account),

        Schema = nameof(DataEntity.Schema.Accounting),

        Entity = typeof(DataEntity.Account),

        Key = nameof(Account.AccountKey)
    };
}