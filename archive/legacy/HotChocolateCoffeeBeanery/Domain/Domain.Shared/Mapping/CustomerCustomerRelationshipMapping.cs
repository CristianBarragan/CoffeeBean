using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class CustomerCustomerRelationshipMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new CustomerCustomerRelationshipMapping("", model.ToString()).Register();
    }
}

public class CustomerCustomerRelationshipMapping
    : BaseMappingRegistration<CustomerCustomerRelationship, DataEntity.CustomerCustomerRelationship>
{
    public CustomerCustomerRelationshipMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(CustomerCustomerRelationship),
            Schema = nameof(DataEntity.Schema.Banking)
        };
        
        map.EntityChildren.AddRange(new LinkKey[]
            {
                new(){
                    AliasFrom    = A(nameof(DataEntity.CustomerCustomerRelationship)),
                    From       = nameof(DataEntity.CustomerCustomerRelationship),
                    FromColumn = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerId),
                    AliasTo    = A(nameof(CustomerMappingType.InnerCustomer), nameof(DataEntity.Customer)),
                    To         = nameof(CustomerMappingType.InnerCustomer),
                    ToColumn   = nameof(DataEntity.Customer.Id)
                },
                new(){
                    AliasFrom    = A(nameof(DataEntity.CustomerCustomerRelationship)),
                    From       = nameof(DataEntity.CustomerCustomerRelationship),
                    FromColumn = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerId),
                    AliasTo    = A(nameof(CustomerMappingType.OuterCustomer), nameof(DataEntity.Customer)),
                    To         = nameof(CustomerMappingType.OuterCustomer),
                    ToColumn   = nameof(DataEntity.Customer.Id)
                }
            }
        );
        
        map.ModelParents.AddRange(new []
        {
            new LinkKey
            {
                AliasFrom    = A(nameof(CustomerCustomerRelationship)),
                From       = nameof(CustomerCustomerRelationship),
                FromColumn = nameof(CustomerCustomerRelationship.InnerCustomerKey),
                AliasTo    = A(nameof(DataEntity.CustomerCustomerRelationship.InnerCustomer)),
                To         = nameof(Customer),
                ToColumn   = nameof(Customer.CustomerKey),
            },
            new LinkKey
            {
                AliasFrom    = A(nameof(CustomerCustomerRelationship)),
                From       = nameof(CustomerCustomerRelationship),
                FromColumn = A(nameof(CustomerCustomerRelationship.OuterCustomerKey)),
                AliasTo    = A(nameof(DataEntity.CustomerCustomerRelationship.OuterCustomer)),
                To         = nameof(Customer),
                ToColumn   = nameof(Customer.CustomerKey),
            }
        });

        map.ModelToEntityLinks.AddRange(new[]
        {
            new LinkKey { AliasFrom    = A(nameof(CustomerCustomerRelationship)), From = nameof(CustomerCustomerRelationship), FromColumn = nameof(CustomerCustomerRelationship.InnerCustomerKey), 
                AliasTo    = A(nameof(CustomerCustomerRelationship.InnerCustomer)),
                To         = nameof(DataEntity.Customer), 
                ToColumn = nameof(DataEntity.Customer.CustomerKey) },
            new LinkKey { AliasFrom    = A(nameof(CustomerCustomerRelationship)), From = nameof(CustomerCustomerRelationship), FromColumn = nameof(CustomerCustomerRelationship.OuterCustomerKey), 
                AliasTo    = A(nameof(CustomerCustomerRelationship.OuterCustomer)),
                To         = nameof(DataEntity.Customer),
                ToColumn = nameof(DataEntity.Customer.CustomerKey) }
        });

        map.UpsertKeys.Add(new UpsertKey(
            nameof(DataEntity.CustomerCustomerRelationship),
            nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey)
        ));

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(DataEntity.CustomerCustomerRelationship.Id),                              DestinationEntity = nameof(CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.Id) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerKey),                DestinationEntity = nameof(CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerKey) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerKey),                DestinationEntity = nameof(CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerKey) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey),            DestinationEntity = nameof(CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey) },
            new FieldMap
            {
                SourceAlias = A(nameof(CustomerCustomerRelationship)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName        = nameof(CustomerCustomerRelationship.CustomerCustomerRelationshipType),
                SourceModel = nameof(CustomerCustomerRelationship),
                DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship),
                DestinationName   = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipType),
                FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { CustomerCustomerRelationshipType.Divorced.ToString(),       (int)CustomerCustomerRelationshipType.Divorced },
                    { CustomerCustomerRelationshipType.Family.ToString(),       (int)CustomerCustomerRelationshipType.Family },
                    { CustomerCustomerRelationshipType.Partner.ToString(),       (int)CustomerCustomerRelationshipType.Divorced },
                    { CustomerCustomerRelationshipType.Single.ToString(),       (int)CustomerCustomerRelationshipType.Single },
                    { CustomerCustomerRelationshipType.Widow.ToString(),       (int)CustomerCustomerRelationshipType.Widow }
                },
                ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { DataEntity.CustomerCustomerRelationshipType.Divorced.ToString(),       (int)DataEntity.CustomerCustomerRelationshipType.Divorced },
                    { DataEntity.CustomerCustomerRelationshipType.Family.ToString(),       (int)DataEntity.CustomerCustomerRelationshipType.Family },
                    { DataEntity.CustomerCustomerRelationshipType.Partner.ToString(),       (int)DataEntity.CustomerCustomerRelationshipType.Divorced },
                    { DataEntity.CustomerCustomerRelationshipType.Single.ToString(),       (int)DataEntity.CustomerCustomerRelationshipType.Single },
                    { DataEntity.CustomerCustomerRelationshipType.Widow.ToString(),       (int)DataEntity.CustomerCustomerRelationshipType.Widow }
                }
            }
        });
        
        map.GraphMap = new GraphMap
        {
            GraphName     = G(nameof(CustomerCustomerEdge)),
            EdgeLabel     = nameof(CustomerCustomerEdge),
            EdgeKeyColumn = nameof(CustomerCustomerEdge.CustomerCustomerRelationshipKey),
            FromVertex = new GraphVertex
            {
                Label     = nameof(Customer),
                KeyColumn = nameof(CustomerCustomerEdge.InnerCustomerKey),
                AliasTo   = A(nameof(Customer))
            },
            ToVertex = new GraphVertex
            {
                Label     = nameof(Customer),
                KeyColumn = nameof(CustomerCustomerEdge.OuterCustomerKey),
                AliasTo   = A(nameof(Customer))
            },
            FromJoinColumn = nameof(Customer.CustomerKey),
            ToJoinColumn   = nameof(Customer.CustomerKey),
        };

        return map;
    }
}