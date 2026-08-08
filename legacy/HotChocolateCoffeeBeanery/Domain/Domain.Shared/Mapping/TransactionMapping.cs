using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;

namespace Domain.Shared.Mapping;
using DataEntity = Database.Entity;

public class TransactionMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new TransactionMapping(type.ToString(), model.ToString()).Register();
    }
}

public class TransactionMapping 
    : BaseMappingRegistration<Transaction, DataEntity.Transaction>
{
    public TransactionMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Transaction),
            Schema = nameof(DataEntity.Schema.Lending)
        };
        
        map.ModelParents.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom    = A(nameof(Transaction)),
                From       = nameof(Transaction),
                AliasTo    = A(nameof(Product)),
                To         = nameof(Product),
            },
            new LinkKey
            {
                AliasFrom    = A(nameof(Transaction)),
                From       = nameof(Transaction),
                AliasTo    = A(nameof(Contract)),
                To         = nameof(Contract),
            },
            new LinkKey
            {
                AliasFrom    = A(nameof(Transaction)),
                From       = nameof(Transaction),
                AliasTo    = A(nameof(Account)),
                To         = nameof(Account),
            }
        });
        
        map.EntityParents.AddRange(new[]
        {
            new LinkKey { AliasFrom    = A(nameof(Transaction)), From = nameof(Transaction), FromColumn = nameof(DataEntity.Transaction.ContractId),
                AliasTo    = A(nameof(DataEntity.Contract)),
                To         = nameof(DataEntity.Contract), 
                ToColumn = nameof(DataEntity.Contract.Id) },
            new LinkKey
            {
                AliasFrom = A(nameof(Transaction)),
                From       = nameof(Transaction),
                FromColumn = nameof(Transaction.ContractKey),
                AliasTo = A(nameof(DataEntity.Contract)),
                To         = nameof(DataEntity.Contract),
                ToColumn   = nameof(DataEntity.Contract.ContractKey)
            },
            new LinkKey { AliasFrom    = A(nameof(Transaction)), From = nameof(Transaction), FromColumn = nameof(DataEntity.Transaction.AccountId),  
                AliasTo    = A(nameof(DataEntity.Account)),
                To         = nameof(DataEntity.Account), ToColumn = nameof(DataEntity.Account.Id) },
            new LinkKey
            {
                AliasFrom = A(nameof(Transaction)),
                From       = nameof(Transaction),
                FromColumn = nameof(Transaction.AccountKey),
                AliasTo = A(nameof(DataEntity.Account)),
                To         = nameof(DataEntity.Account),
                ToColumn   = nameof(DataEntity.Account.AccountKey)
            },
            new LinkKey
            {
                AliasFrom = A(nameof(Transaction)),
                From       = nameof(Transaction),
                FromColumn = nameof(Transaction.AccountKey),
                AliasTo = A(nameof(DataEntity.Account)),
                To         = nameof(DataEntity.Account),
                ToColumn   = nameof(DataEntity.Account.AccountKey)
            }
        });


        map.ModelToEntityLinks.Add(new LinkKey
        {
            AliasFrom    = A(nameof(Transaction)),
            From       = nameof(Transaction),
            FromColumn = nameof(Transaction.TransactionKey),
            AliasTo    = A(nameof(DataEntity.Transaction)),
            To         = nameof(DataEntity.Transaction),
            ToColumn   = nameof(DataEntity.Transaction.TransactionKey)
        });

        map.UpsertKeys.Add(new UpsertKey(
            nameof(DataEntity.Transaction),
            nameof(DataEntity.Transaction.TransactionKey)
        ));

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(Transaction)), DestinationAlias = A(nameof(DataEntity.Transaction)), SourceName = nameof(DataEntity.Transaction.Id),  DestinationEntity = nameof(DataEntity.Transaction), DestinationName = nameof(DataEntity.Transaction.Id) },
            new FieldMap { SourceAlias = A(nameof(Transaction)), DestinationAlias = A(nameof(DataEntity.Transaction)), SourceName = nameof(Transaction.TransactionKey), DestinationEntity = nameof(DataEntity.Transaction), DestinationName = nameof(DataEntity.Transaction.TransactionKey) },
            new FieldMap { SourceAlias = A(nameof(Transaction)), DestinationAlias = A(nameof(DataEntity.Transaction)), SourceName = nameof(Transaction.Amount),         DestinationEntity = nameof(DataEntity.Transaction), DestinationName = nameof(DataEntity.Transaction.Amount) },
            new FieldMap { SourceAlias = A(nameof(Transaction)), DestinationAlias = A(nameof(DataEntity.Transaction)), SourceName = nameof(Transaction.Balance),        DestinationEntity = nameof(DataEntity.Transaction), DestinationName = nameof(DataEntity.Transaction.Balance) },
            new FieldMap { SourceAlias = A(nameof(Transaction)), DestinationAlias = A(nameof(DataEntity.Transaction)), SourceName = nameof(Transaction.Account),        DestinationEntity = nameof(DataEntity.Transaction), DestinationName = nameof(DataEntity.Transaction.Account) },
            new FieldMap { SourceAlias = A(nameof(Transaction)), DestinationAlias = A(nameof(DataEntity.Transaction)), SourceName = nameof(Transaction.Contract),        DestinationEntity = nameof(DataEntity.Transaction), DestinationName = nameof(DataEntity.Transaction.Contract) }
        });

        return map;
    }
}
