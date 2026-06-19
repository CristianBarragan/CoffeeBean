using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class ProductMappingSet : IMappingSet
{
    private static bool _registered;

    public void Register()
    {
        if (_registered)
            return;

        new ProductMapping().Register();

        _registered = true;
    }
}

public sealed partial class ProductMapping : BaseMappingRegistration<Product>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Product)
        };

        map.AddModelToEntity<Product, DataEntity.Account>(
            m => m.AccountKey,
            e => e.AccountKey,
            isPrimary: true);

        map.AddModelToEntity<Product, DataEntity.Contract>(
            m => m.ContractKey,
            e => e.ContractKey);

        map.AddModelToEntity<Product, DataEntity.Transaction>(
            m => m.TransactionKey,
            e => e.TransactionKey);

        map.AddModelToEntity<Product, DataEntity.CustomerBankingRelationship>(
            m => m.CustomerBankingRelationshipKey,
            e => e.CustomerBankingRelationshipKey);

        return map;
    }
}