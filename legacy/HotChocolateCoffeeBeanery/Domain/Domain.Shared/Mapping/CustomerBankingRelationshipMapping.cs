using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class CustomerBankingRelationshipMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new CustomerBankingRelationshipMapping(type.ToString(), model.ToString()).Register();
    }
}

public class CustomerBankingRelationshipMapping
    : BaseMappingRegistration<CustomerBankingRelationship, DataEntity.CustomerBankingRelationship>
{
    public CustomerBankingRelationshipMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(CustomerBankingRelationship),
            Schema = nameof(DataEntity.Schema.Banking)
        };

        map.EntityParents.AddRange(new []
            {
                new LinkKey
                {
                    AliasFrom    = A(nameof(DataEntity.CustomerBankingRelationship)),
                    From       = nameof(DataEntity.CustomerBankingRelationship),
                    FromColumn = nameof(DataEntity.CustomerBankingRelationship.CustomerId),
                    AliasTo    = A(nameof(DataEntity.Customer)),
                    To         = nameof(DataEntity.Customer),
                    ToColumn   = nameof(DataEntity.Customer.Id)
                },
                new LinkKey
                {
                    AliasFrom = A(nameof(DataEntity.CustomerBankingRelationship)),
                    From       = nameof(DataEntity.CustomerBankingRelationship),
                    FromColumn = nameof(DataEntity.CustomerBankingRelationship.CustomerKey),
                    AliasTo = A(nameof(DataEntity.Customer)),
                    To         = nameof(DataEntity.Customer),
                    ToColumn   = nameof(DataEntity.Customer.CustomerKey)
                }
            }
        );

        map.EntityChildren.Add(new LinkKey
        {
            AliasFrom    = A(nameof(DataEntity.CustomerBankingRelationship)),
            From       = nameof(DataEntity.CustomerBankingRelationship),
            FromColumn = nameof(DataEntity.CustomerBankingRelationship.Id),
            AliasTo    = A(nameof(DataEntity.Contract)),
            To         = nameof(DataEntity.Contract),
            ToColumn   = nameof(DataEntity.Contract.CustomerBankingRelationshipId)
        });

        // map.ModelParents.AddRange(new LinkKey[]
        //     {
        //         new(){
        //             AliasFrom    = A(nameof(DataEntity.CustomerBankingRelationship)),
        //             From       = nameof(DataEntity.CustomerBankingRelationship),
        //             FromColumn = nameof(DataEntity.CustomerBankingRelationship.CustomerId),
        //             AliasTo    = A(nameof(DataEntity.Customer)),
        //             To         = nameof(DataEntity.Customer),
        //             ToColumn   = nameof(DataEntity.Customer.Id)
        //         }
        //     }
        // );
        //
        // map.ModelChildren.AddRange(new[]
        //     {
        //         new LinkKey
        //         {
        //             AliasFrom    = A(nameof(CustomerBankingRelationship)),
        //             From       = nameof(CustomerBankingRelationship),
        //             FromColumn = nameof(CustomerBankingRelationship.CustomerBankingRelationshipKey),
        //             AliasTo    = A(nameof(DataEntity.Contract)),
        //             To         = nameof(DataEntity.Contract),
        //             ToColumn   = nameof(Contract.CustomerBankingRelationshipKey),
        //         }
        //     }
        //     
        // );

        map.ModelToEntityLinks.Add(new LinkKey
        {
            AliasFrom    = A(nameof(CustomerBankingRelationship)),
            From       = nameof(CustomerBankingRelationship),
            FromColumn = nameof(CustomerBankingRelationship.CustomerBankingRelationshipKey),
            AliasTo    = A(nameof(DataEntity.CustomerBankingRelationship)),
            To         = nameof(DataEntity.CustomerBankingRelationship),
            ToColumn   = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
        });

        map.UpsertKeys.Add(new UpsertKey(
            nameof(DataEntity.CustomerBankingRelationship),
            nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey)
        ));

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(CustomerBankingRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerBankingRelationship)), SourceName = nameof(DataEntity.CustomerBankingRelationship.Id),                       DestinationEntity = nameof(DataEntity.CustomerBankingRelationship), DestinationName = nameof(DataEntity.CustomerBankingRelationship.Id) },
            new FieldMap { SourceAlias = A(nameof(CustomerBankingRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerBankingRelationship)), SourceName = nameof(CustomerBankingRelationship.CustomerBankingRelationshipKey),       DestinationEntity = nameof(DataEntity.CustomerBankingRelationship), DestinationName = nameof(DataEntity.CustomerBankingRelationship.CustomerBankingRelationshipKey) },
            new FieldMap { SourceAlias = A(nameof(CustomerBankingRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerBankingRelationship)), SourceName = nameof(CustomerBankingRelationship.CustomerKey),                         DestinationEntity = nameof(DataEntity.CustomerBankingRelationship), DestinationName = nameof(DataEntity.CustomerBankingRelationship.CustomerKey) }
        });

        return map;
    }
}