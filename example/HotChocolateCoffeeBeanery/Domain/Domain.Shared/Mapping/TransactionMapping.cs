using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class TransactionMappingSet : IMappingSet
{
    public void Register()
    {
        new TransactionMapping().Register();
    }
}

public sealed partial class TransactionMapping : BaseMappingRegistration<Transaction>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Transaction),
            Schema = nameof(DataEntity.Schema.Lending)
        };

        map.AddModelToEntity<Transaction, DataEntity.Transaction>(
            m => m.TransactionKey,
            e => e.TransactionKey,
            isPrimary: true);

        map.UpsertKeys.Add(
            new UpsertKey(
                nameof(DataEntity.Transaction),
                nameof(DataEntity.Transaction.TransactionKey)));

        return map;
    }
}