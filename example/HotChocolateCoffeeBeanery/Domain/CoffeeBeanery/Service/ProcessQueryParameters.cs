using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.Service
{
    public sealed class ProcessQueryParameters
    {
        public _SqlCompilationContext Context { get; set; } = new();
        public string Model { get; set; }
    }
}