using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class CustomerCustomerEdgeMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new CustomerCustomerEdgeMapping("", model.ToString()).Register();
    }
}

public class CustomerCustomerEdgeMapping
    : BaseModelMappingRegistration<CustomerCustomerEdge>
{
    public CustomerCustomerEdgeMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap()
        {
            ModelName = nameof(CustomerCustomerEdge),
            IsRoot = true
        };

        map.ModelChildren.AddRange(new[]
        {
            new LinkKey { AliasFrom = A(nameof(CustomerCustomerEdge)), From = nameof(CustomerCustomerEdge), FromColumn = nameof(CustomerCustomerEdge.CustomerCustomerRelationshipKey), AliasTo = A(nameof(CustomerCustomerRelationship)), To = nameof(CustomerCustomerRelationship), ToColumn = nameof(CustomerCustomerRelationship.CustomerCustomerRelationshipKey) },
            new LinkKey { AliasFrom = A(nameof(CustomerCustomerEdge)), From = nameof(CustomerCustomerEdge), FromColumn = nameof(CustomerCustomerEdge.InnerCustomerKey), AliasTo = A(nameof(CustomerCustomerRelationship)), To = A(nameof(CustomerCustomerRelationship.InnerCustomer), nameof(Customer)), ToColumn = nameof(CustomerCustomerRelationship.InnerCustomerKey) },
            new LinkKey { AliasFrom = A(nameof(CustomerCustomerEdge)), From = nameof(CustomerCustomerEdge), FromColumn = nameof(CustomerCustomerEdge.OuterCustomerKey), AliasTo = A(nameof(CustomerCustomerRelationship)), To = A(nameof(CustomerCustomerRelationship.OuterCustomer), nameof(Customer)), ToColumn = nameof(CustomerCustomerRelationship.OuterCustomerKey) }
        });

        map.ModelParents.Add(new LinkKey
        {
            AliasFrom = A(nameof(CustomerCustomerEdge)),
            From      = nameof(CustomerCustomerEdge),
            AliasTo   = A(nameof(Wrapper)),
            To        = nameof(Wrapper)
        });

        map.ModelToEntityLinks.AddRange(new[]
        {
            new LinkKey { AliasFrom = A(nameof(CustomerCustomerEdge)), From = nameof(CustomerCustomerEdge), FromColumn = nameof(CustomerCustomerEdge.InnerCustomer),                AliasTo = A(nameof(DataEntity.CustomerCustomerRelationship)), To = nameof(DataEntity.CustomerCustomerRelationship), ToColumn = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerKey) },
            new LinkKey { AliasFrom = A(nameof(CustomerCustomerEdge)), From = nameof(CustomerCustomerEdge), FromColumn = nameof(CustomerCustomerEdge.OuterCustomer),                AliasTo = A(nameof(DataEntity.CustomerCustomerRelationship)), To = nameof(DataEntity.CustomerCustomerRelationship), ToColumn = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerKey) },
            new LinkKey { AliasFrom = A(nameof(CustomerCustomerEdge)), From = nameof(CustomerCustomerEdge), FromColumn = nameof(CustomerCustomerEdge.CustomerCustomerRelationship), AliasTo = A(nameof(DataEntity.CustomerCustomerRelationship)), To = nameof(DataEntity.CustomerCustomerRelationship), ToColumn = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey) },
        });

        map.UpsertKeys.AddRange(new[]
        {
            new UpsertKey(nameof(DataEntity.CustomerCustomerRelationship), nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerKey)),
            new UpsertKey(nameof(DataEntity.CustomerCustomerRelationship), nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerKey)),
            new UpsertKey(nameof(DataEntity.CustomerCustomerRelationship), nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey))
        });

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerEdge)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(CustomerCustomerEdge.InnerCustomerKey),                DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerKey) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerEdge)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(CustomerCustomerEdge.InnerCustomer),      DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomer) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerEdge)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(CustomerCustomerEdge.OuterCustomerKey),                DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerKey) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerEdge)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(CustomerCustomerEdge.OuterCustomer),      DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomer) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerEdge)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(CustomerCustomerEdge.CustomerCustomerRelationshipKey), DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey) },
            new FieldMap { SourceAlias = A(nameof(CustomerCustomerEdge)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName = nameof(CustomerCustomerEdge.GraphModel.EdgeKey), DestinationEntity = nameof(DataEntity.CustomerCustomerRelationship), DestinationName = nameof(DataEntity.CustomerCustomerRelationship.CustomerCustomerRelationshipKey) },
            new FieldMap
            {
                SourceAlias = A(nameof(CustomerCustomerEdge)), DestinationAlias = A(nameof(DataEntity.CustomerCustomerRelationship)), SourceName        = nameof(CustomerCustomerRelationship.CustomerCustomerRelationshipType),
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
        return map;
    }
}