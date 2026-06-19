using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class AccountMappingSet : IMappingSet
{
    public void Register()
    {
        new AccountMapping().Register();
    }
}

public sealed partial class AccountMapping : BaseMappingRegistration<Account>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Account),
            Schema = nameof(DataEntity.Schema.Account),
            PrimaryKey = nameof(DataEntity.Account.Id)
        };

        map.AddModelToEntity<Account, DataEntity.Account>(
            m => m.AccountKey,
            e => e.AccountKey);

        map.UpsertKeys.Add(
            new UpsertKey(
                nameof(DataEntity.Account),
                nameof(DataEntity.Account.AccountKey)));

        return map;
    }
}