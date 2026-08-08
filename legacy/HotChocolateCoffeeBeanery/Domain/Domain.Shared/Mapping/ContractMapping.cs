using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class ContractMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new ContractMapping(type.ToString(), model.ToString()).Register();
    }
}

public class ContractMapping : BaseMappingRegistration<Contract, DataEntity.Contract>
{
    public ContractMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Contract),
            Schema = nameof(DataEntity.Schema.Lending)
        };

        map.EntityParents.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.Contract)),
                From       = nameof(DataEntity.Contract),
                FromColumn = nameof(DataEntity.Contract.CustomerBankingRelationshipId),
                AliasTo    = A(nameof(DataEntity.CustomerBankingRelationship)),
                To         = nameof(DataEntity.CustomerBankingRelationship),
                ToColumn   = nameof(DataEntity.CustomerBankingRelationship.Id)
            },
            new LinkKey
            {
                AliasFrom = A(nameof(DataEntity.Contract)),
                From       = nameof(DataEntity.Contract),
                FromColumn = nameof(DataEntity.Contract.CustomerBankingRelationshipKey),
                AliasTo = A(nameof(DataEntity.CustomerBankingRelationship)),
                To         = nameof(DataEntity.CustomerBankingRelationship),
                ToColumn   = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
            }
        });

        map.EntityRelatedChildren.Add(new LinkKey
        {
            AliasFrom    = A(nameof(DataEntity.Contract)),
            From       = nameof(DataEntity.Contract),
            FromColumn = nameof(DataEntity.Contract.AccountId),
            AliasTo    = A(nameof(DataEntity.Account)),
            To         = nameof(DataEntity.Account),
            ToColumn   = nameof(DataEntity.Account.Id)
        });

        map.ModelParents.Add(new LinkKey
        {
            AliasFrom    = A(nameof(Contract)),
            From       = nameof(Contract),
            AliasTo    = A(nameof(Product)),
            To         = nameof(Product),
        });
        
        map.ModelChildren.Add(new LinkKey
        {
            AliasFrom    = A(nameof(Contract)),
            From       = nameof(Contract),
            FromColumn = nameof(Contract.ContractKey),
            AliasTo    = A(nameof(DataEntity.Transaction)),
            To         = nameof(DataEntity.Transaction),
            ToColumn   = nameof(Transaction.ContractKey)
        });

        map.ModelToEntityLinks.Add(new LinkKey
        {
            AliasFrom    = A(nameof(Contract)),
            From       = nameof(Contract),
            FromColumn = nameof(Contract.ContractKey),
            AliasTo    = A(nameof(DataEntity.Contract)),
            To         = nameof(DataEntity.Contract),
            ToColumn   = nameof(DataEntity.Contract.ContractKey)
        });

        map.UpsertKeys.Add(new UpsertKey(
            nameof(DataEntity.Contract),
            nameof(DataEntity.Contract.ContractKey)
        ));

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(Contract)), DestinationAlias = A(nameof(DataEntity.Contract)), SourceName = nameof(DataEntity.Contract.Id), DestinationEntity = nameof(DataEntity.Contract), DestinationName = nameof(DataEntity.Contract.Id) },
            new FieldMap { SourceAlias = A(nameof(Contract)), DestinationAlias = A(nameof(DataEntity.Contract)), SourceName = nameof(Contract.ContractKey),   DestinationEntity = nameof(DataEntity.Contract), DestinationName = nameof(DataEntity.Contract.ContractKey) },
            new FieldMap
            {
                SourceAlias = A(nameof(Product)), DestinationAlias = A(nameof(DataEntity.Contract)), SourceName = nameof(Product.ProductType),  DestinationEntity = nameof(DataEntity.Contract), DestinationName = nameof(DataEntity.Contract.ContractType),
                FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { ContractType.CreditCard.ToString(), (int)ContractType.CreditCard },
                    { ContractType.Mortgage.ToString(), (int)ContractType.Mortgage },
                    { ContractType.PersonalLoan.ToString(), (int)ContractType.PersonalLoan }
                },
                ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { ProductType.CreditCard.ToString(), (int)ProductType.CreditCard },
                    { ProductType.Mortgage.ToString(), (int)ProductType.Mortgage },
                    { ProductType.PersonalLoan.ToString(), (int)ProductType.PersonalLoan }
                }
            },
            new FieldMap { SourceAlias = A(nameof(Contract)), DestinationAlias = A(nameof(DataEntity.Contract)), SourceName = nameof(Contract.Amount),        DestinationEntity = nameof(DataEntity.Contract), DestinationName = nameof(DataEntity.Contract.Amount) },
            new FieldMap { SourceAlias = A(nameof(Contract)), DestinationAlias = A(nameof(DataEntity.Contract)), SourceName = nameof(Contract.Transaction), DestinationEntity = nameof(DataEntity.Contract), DestinationName = nameof(DataEntity.Contract.Transaction) }
        });

        return map;
    }
}