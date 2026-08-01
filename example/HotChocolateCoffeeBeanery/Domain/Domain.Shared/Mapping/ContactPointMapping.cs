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

        Entities =
        [
            new()
            {
                Entity = typeof(DataEntity.ContactPoint),

                ModelKey =
                    nameof(ContactPoint.ContactPointKey),

                EntityKey =
                    nameof(DataEntity.ContactPoint.ContactPointKey),

                IsPrimary = true
            }
        ],
        PrimaryKey = [new()
        {
            Entity = typeof(DataEntity.ContactPoint),
        
            // ModelKey = nameof(ContactPoint.ContactPointKey),
            ColumnKey = 
                nameof(DataEntity.ContactPoint.Id)
        }],
        UpsertKeys =
        [
            new()
            {
                Entity = typeof(DataEntity.ContactPoint),

                Column =
                    nameof(DataEntity.ContactPoint.ContactPointKey)
            }
        ],

        Fields =
        [
            new()
            {
                Source =
                    nameof(ContactPoint.ContactPointType),

                Entity =
                    typeof(DataEntity.ContactPoint),

                Destination =
                    nameof(DataEntity.ContactPoint.ContactPointType),

                EnumMapping =
                    new EnumMappingDefinition<
                        ContactPointType,
                        DataEntity.ContactPointType>()
            }
        ]
    };
}