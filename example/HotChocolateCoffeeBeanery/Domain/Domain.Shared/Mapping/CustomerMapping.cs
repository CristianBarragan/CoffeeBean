using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public class CustomerMappingSet : IMappingSet
{
    public void Register()
    {
        new CustomerMapping().Register();
    }
}

public partial class CustomerMapping : BaseMappingRegistration<Customer>
{
    protected override NodeMap BuildMap()
    {
        var map = new NodeMap
        {
            ModelName = nameof(Customer),
            Schema = nameof(DataEntity.Schema.Banking)
        };

        map.AddModelToEntity<Customer, DataEntity.Customer>(
            m => m.CustomerKey,
            e => e.CustomerKey,
            isPrimary: true);

        map.UpsertKeys.Add(
            new UpsertKey(
                nameof(DataEntity.Customer),
                nameof(DataEntity.Customer.CustomerKey)));

        map.FieldMaps.Add(new FieldMap
        {
            SourceName = nameof(Customer.CustomerType),
            DestinationEntity = nameof(DataEntity.Customer),
            DestinationName = nameof(DataEntity.Customer.CustomerType),

            FromEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { CustomerType.Person.ToString(), (int)CustomerType.Person },
                { CustomerType.Organisation.ToString(), (int)CustomerType.Organisation }
            },

            ToEnum = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { DataEntity.CustomerType.Person.ToString(), (int)DataEntity.CustomerType.Person },
                { DataEntity.CustomerType.Organisation.ToString(), (int)DataEntity.CustomerType.Organisation }
            }
        });

        map.FieldMaps.Add(new FieldMap
        {
            SourceName = nameof(Customer.FirstNaming),
            DestinationEntity = nameof(DataEntity.Customer),
            DestinationName = nameof(DataEntity.Customer.FirstName)
        });

        map.FieldMaps.Add(new FieldMap
        {
            SourceName = nameof(Customer.LastNaming),
            DestinationEntity = nameof(DataEntity.Customer),
            DestinationName = nameof(DataEntity.Customer.LastName)
        });

        map.FieldMaps.Add(new FieldMap
        {
            SourceName = nameof(Customer.FullNaming),
            DestinationEntity = nameof(DataEntity.Customer),
            DestinationName = nameof(DataEntity.Customer.FullName)
        });

        return map;
    }
}