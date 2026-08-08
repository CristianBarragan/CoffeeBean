namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public interface IMappingSet<TEnum, T2Enum>
        where TEnum : Enum
        where T2Enum : Enum 
    {
        void Register(TEnum type, T2Enum model);
    }
}