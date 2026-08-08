using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class AccountMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new AccountMapping(type.ToString(), model.ToString()).Register();
    }
}

public class AccountMapping : BaseMappingRegistration<Account, DataEntity.Account>
{
    public AccountMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Account),
            Schema = nameof(DataEntity.Schema.Account)
        };

        map.EntityRelatedParents.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.Account)),
                From       = nameof(DataEntity.Account),
                FromColumn = nameof(DataEntity.Account.Id),
                AliasTo    = A(nameof(Contract)),
                To         = nameof(Contract),
                ToColumn   = nameof(DataEntity.Contract.AccountId)
            },
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.Account)),
                From       = nameof(DataEntity.Account),
                FromColumn = nameof(DataEntity.Account.AccountKey),
                AliasTo    = A(nameof(Contract)),
                To         = nameof(Contract),
                ToColumn   = nameof(DataEntity.Contract.AccountKey)
            }
        });

        map.EntityRelatedChildren.Add(new LinkKey
        {
            AliasFrom    = A(nameof(DataEntity.Account)),
            From       = nameof(DataEntity.Account),
            FromColumn = nameof(DataEntity.Account.Id),
            AliasTo    = A(nameof(DataEntity.Transaction)),
            To         = nameof(DataEntity.Transaction),
            ToColumn   = nameof(DataEntity.Transaction.AccountId)
        });

        map.ModelParents.Add(new LinkKey
        {
            AliasFrom    = A(nameof(Account)),
            From       = nameof(Account),
            FromColumn = nameof(DataEntity.Account.AccountKey),
            AliasTo    = A(nameof(Product)),
            To         = nameof(Product),
            ToColumn   = nameof(Product.AccountKey)
        });
        
        map.ModelChildren.Add(new LinkKey
        {
            AliasFrom    = A(nameof(Account)),
            From       = nameof(Account),
            FromColumn = nameof(DataEntity.Account.AccountKey),
            AliasTo    = A(nameof(Transaction)),
            To         = nameof(Transaction),
            ToColumn   = nameof(Transaction.AccountKey)
        });

        map.ModelToEntityLinks.Add(new LinkKey
        {
            AliasFrom    = A(nameof(Account)),
            From       = nameof(Account),
            FromColumn = nameof(Account.AccountKey),
            AliasTo    = A(nameof(DataEntity.Account)),
            To         = nameof(DataEntity.Account),
            ToColumn   = nameof(DataEntity.Account.AccountKey)
        });

        map.UpsertKeys.Add(new UpsertKey(
            nameof(DataEntity.Account),
            nameof(DataEntity.Account.AccountKey)
        ));

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(Account)), DestinationAlias = A(nameof(DataEntity.Account)), SourceName = nameof(DataEntity.Account.Id), DestinationEntity = nameof(DataEntity.Account), DestinationName = nameof(DataEntity.Account.Id) },
            new FieldMap { SourceAlias = A(nameof(Account)), DestinationAlias = A(nameof(DataEntity.Account)), SourceName = nameof(Account.AccountKey), DestinationEntity = nameof(DataEntity.Account), DestinationName = nameof(DataEntity.Account.AccountKey) },
            new FieldMap { SourceAlias = A(nameof(Account)), DestinationAlias = A(nameof(DataEntity.Account)), SourceName = nameof(Account.AccountNumber), DestinationEntity = nameof(DataEntity.Account), DestinationName = nameof(DataEntity.Account.AccountNumber) },
            new FieldMap { SourceAlias = A(nameof(Account)), DestinationAlias = A(nameof(DataEntity.Account)), SourceName = nameof(Account.AccountName), DestinationEntity = nameof(DataEntity.Account), DestinationName = nameof(DataEntity.Account.AccountName) },
            new FieldMap { SourceAlias = A(nameof(Account)), DestinationAlias = A(nameof(DataEntity.Account)), SourceName = nameof(Account.Contract), DestinationEntity = nameof(DataEntity.Account), DestinationName = nameof(DataEntity.Account.Contract) },
            new FieldMap { SourceAlias = A(nameof(Account)), DestinationAlias = A(nameof(DataEntity.Account)), SourceName = nameof(Account.Transaction), DestinationEntity = nameof(DataEntity.Account), DestinationName = nameof(DataEntity.Account.Transaction) }
        });

        return map;
    }
}