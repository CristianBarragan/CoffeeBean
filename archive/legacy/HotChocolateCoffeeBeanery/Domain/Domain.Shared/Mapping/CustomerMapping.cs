using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class InnerCustomerMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new InnerCustomerMapping(type.ToString(), model.ToString()).Register();
    }
}

public class OuterCustomerMappingSet : IMappingSet<CustomerMappingType, Domain.Model.Model>
{
    public void Register(CustomerMappingType type, Domain.Model.Model model)
    {
        new OuterCustomerMapping(type.ToString(), model.ToString()).Register();
    }
}

public class InnerCustomerMapping : CustomerBaseMapping<InnerCustomerMapping>
{
    public InnerCustomerMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = base.BuildMap();
        map.ModelName = nameof(CustomerMappingType.InnerCustomer);
        
        map.EntityParents.AddRange(new[]
        {   
            new LinkKey
            {
                AliasFrom  = A(nameof(DataEntity.Customer)),
                From       = nameof(DataEntity.Customer),
                FromColumn = nameof(DataEntity.Customer.Id),
                AliasTo    = nameof(DataEntity.CustomerCustomerRelationship),
                To         = nameof(DataEntity.CustomerCustomerRelationship),
                ToColumn   = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerId)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(DataEntity.Customer)),
                From       = nameof(DataEntity.Customer),
                FromColumn = nameof(DataEntity.Customer.CustomerKey),
                AliasTo    = nameof(DataEntity.CustomerCustomerRelationship),
                To         = nameof(DataEntity.CustomerCustomerRelationship),
                ToColumn   = nameof(DataEntity.CustomerCustomerRelationship.InnerCustomerKey)
            }
        });

         map.ModelToEntityLinks.AddRange(new[]
         {
             new LinkKey { AliasFrom = A(nameof(Customer)), From = nameof(Customer), FromColumn = nameof(Customer.CustomerKey), AliasTo = A(nameof(DataEntity.Customer)), 
                 To = nameof(DataEntity.Customer), ToColumn = nameof(DataEntity.Customer.CustomerKey) },
         });
         
         return map;
    }
}

public class OuterCustomerMapping : CustomerBaseMapping<OuterCustomerMapping>
{
    public OuterCustomerMapping(string alias, string model) : base(alias, model)
    {
    }
    
    protected override NodeMap BuildMap()
    {
        var map = base.BuildMap();
        map.ModelName = nameof(CustomerMappingType.OuterCustomer);
        
        map.EntityParents.AddRange(new[]
        {   
            new LinkKey
            {
                AliasFrom  = A(nameof(DataEntity.Customer)),
                From       = nameof(DataEntity.Customer),
                FromColumn = nameof(DataEntity.Customer.Id),
                AliasTo    = nameof(DataEntity.CustomerCustomerRelationship),
                To         = nameof(DataEntity.CustomerCustomerRelationship),
                ToColumn   = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerId)
            },
            new LinkKey
            {
                AliasFrom  = A(nameof(DataEntity.Customer)),
                From       = nameof(DataEntity.Customer),
                FromColumn = nameof(DataEntity.Customer.CustomerKey),
                AliasTo    = nameof(DataEntity.CustomerCustomerRelationship),
                To         = nameof(DataEntity.CustomerCustomerRelationship),
                ToColumn   = nameof(DataEntity.CustomerCustomerRelationship.OuterCustomerKey)
            }
        });

        map.ModelToEntityLinks.AddRange(new[]
        {
            new LinkKey { AliasFrom = A(nameof(Customer)), From = nameof(Customer), FromColumn = nameof(Customer.CustomerKey), 
                AliasTo = A(nameof(DataEntity.Customer)), To = nameof(DataEntity.Customer), ToColumn = nameof(DataEntity.Customer.CustomerKey) }
        });
         
        return map;
    }
}

public abstract class CustomerBaseMapping<TAlias>
    : BaseMappingRegistration<Customer, DataEntity.Customer>
{
    protected CustomerBaseMapping(string alias, string model) : base(alias, model)
    {
    }

    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            Schema = nameof(DataEntity.Schema.Banking)
        };
        
        map.EntityChildren.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.Customer)), 
                From       = nameof(DataEntity.Customer),
                FromColumn = nameof(DataEntity.Customer.Id),
                AliasTo    = A(nameof(DataEntity.ContactPoint)),
                To         = nameof(DataEntity.ContactPoint),
                ToColumn   = nameof(DataEntity.ContactPoint.CustomerId)
            },
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.Customer)),
                From       = nameof(DataEntity.Customer),
                FromColumn = nameof(DataEntity.Customer.Id),
                AliasTo    = A(nameof(DataEntity.CustomerBankingRelationship)),
                To         = nameof(DataEntity.CustomerBankingRelationship),
                ToColumn   = nameof(DataEntity.CustomerBankingRelationship.CustomerId)
            }
        });
        
        map.ModelChildren.AddRange(new[]
        {
            new LinkKey
            {
                AliasFrom    = A(nameof(DataEntity.Customer)), 
                From       = nameof(DataEntity.Customer),
                AliasTo    = A(nameof(Product)),
                To         = nameof(Product)
            }
        });

        map.UpsertKeys.Add(new UpsertKey(
            nameof(DataEntity.Customer),
            nameof(DataEntity.Customer.CustomerKey)
        ));

        map.FieldMaps.AddRange(new[]
        {
            new FieldMap { SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.Customer)), SourceName = nameof(DataEntity.Customer.Id), DestinationEntity = nameof(DataEntity.Customer), DestinationName = nameof(DataEntity.Customer.Id) },
            new FieldMap { SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.Customer)), SourceName = nameof(Customer.CustomerKey),   DestinationEntity = nameof(DataEntity.Customer), DestinationName = nameof(DataEntity.Customer.CustomerKey) },
            new FieldMap
            {
                SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.Customer)), SourceName        = nameof(Customer.CustomerType),
                DestinationEntity = nameof(DataEntity.Customer),
                DestinationName   = nameof(DataEntity.Customer.CustomerType),
                FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { CustomerType.Person.ToString(),       (int)CustomerType.Person },
                    { CustomerType.Organisation.ToString(), (int)CustomerType.Organisation }
                },
                ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { DataEntity.CustomerType.Person.ToString(),       (int)DataEntity.CustomerType.Person },
                    { DataEntity.CustomerType.Organisation.ToString(), (int)DataEntity.CustomerType.Organisation }
                }
            },
            new FieldMap { SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.Customer)), SourceName = nameof(Customer.FirstNaming), DestinationEntity = nameof(DataEntity.Customer), DestinationName = nameof(DataEntity.Customer.FirstName) },
            new FieldMap { SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.Customer)), SourceName = nameof(Customer.LastNaming),  DestinationEntity = nameof(DataEntity.Customer), DestinationName = nameof(DataEntity.Customer.LastName) },
            new FieldMap { SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.Customer)), SourceName = nameof(Customer.FullNaming),  DestinationEntity = nameof(DataEntity.Customer), DestinationName = nameof(DataEntity.Customer.FullName) },
            new FieldMap { SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.ContactPoint)), SourceName = nameof(Customer.ContactPoint),  DestinationEntity = nameof(DataEntity.Customer), DestinationName = nameof(DataEntity.Customer.ContactPoint) },
            new FieldMap { SourceAlias = A(nameof(Customer)), DestinationAlias = A(nameof(DataEntity.CustomerBankingRelationship)), SourceName = nameof(Customer.Product),  DestinationEntity = nameof(DataEntity.Customer), DestinationName = nameof(DataEntity.Customer.CustomerBankingRelationship) }
        });

        return map;
    }
}