using System;
using System.Collections.Generic;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

public abstract class EntityFilterMetadata
{
    public sealed class Field : EntityFilterMetadata
    {
        public RuntimeEntityMetadata Entity { get; }
        public RuntimeFieldMetadata FieldMetadata { get; }
        public FilterOperator Operator { get; }
        public object? Value { get; }

        public Field(
            RuntimeEntityMetadata entity,
            RuntimeFieldMetadata field,
            FilterOperator op,
            object? value)
        {
            Entity = entity;
            FieldMetadata = field;
            Operator = op;
            Value = value;
        }
    }


    public sealed class Navigation : EntityFilterMetadata
    {
        public RuntimeNavigationMetadata NavigationMetadata { get; }
        public EntityFilterMetadata Inner { get; }

        public Navigation(
            RuntimeNavigationMetadata navigation,
            EntityFilterMetadata inner)
        {
            NavigationMetadata = navigation;
            Inner = inner;
        }
    }


    public sealed class Collection : EntityFilterMetadata
    {
        public FilterOperator Operator { get; }
        public EntityFilterMetadata Inner { get; }

        public Collection(
            FilterOperator op,
            EntityFilterMetadata inner)
        {
            Operator = op;
            Inner = inner;
        }
    }


    public sealed class And : EntityFilterMetadata
    {
        public IReadOnlyList<EntityFilterMetadata> Items { get; }

        public And(
            IReadOnlyList<EntityFilterMetadata> items)
        {
            Items = items;
        }
    }


    public sealed class Or : EntityFilterMetadata
    {
        public IReadOnlyList<EntityFilterMetadata> Items { get; }

        public Or(
            IReadOnlyList<EntityFilterMetadata> items)
        {
            Items = items;
        }
    }
}