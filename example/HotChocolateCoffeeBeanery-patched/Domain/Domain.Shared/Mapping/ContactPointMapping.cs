using CoffeeBeanery.GraphQL.Core.Mapping;
using Domain.Model;
using DataEntity = Database.Entity;

namespace Domain.Shared.Mapping;

public partial class ContactPointMapping : IMappingDefinition
{
    public MappingDefinition Definition => new()
    {
        Model = typeof(ContactPoint),

        Schema = nameof(DataEntity.Schema.Banking),

        Entity = typeof(DataEntity.ContactPoint),

        Key = nameof(ContactPoint.ContactPointKey),

        Fields =
        [
            new()
            {
                Source = nameof(ContactPoint.ContactPointType),

                Entity = typeof(DataEntity.ContactPoint),

                Destination = nameof(DataEntity.ContactPoint.ContactPointType),

                EnumMapping =
                    new EnumMappingDefinition<
                        ContactPointType,
                        DataEntity.ContactPointType>()
            }
        ]
    };
}