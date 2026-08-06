using System;
using System.Linq.Expressions;

namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public sealed class LinkMap
    {
        public string SourceKey { get; set; }
        public string EntityKey { get; set; }
    }
}