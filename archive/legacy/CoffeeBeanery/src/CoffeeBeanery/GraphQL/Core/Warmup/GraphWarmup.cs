using System.Reflection;
using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Core.Warmup;
using CoffeeBeanery.GraphQL.Helper;

public static class GraphWarmup
{
    private static bool _initialized;

    public static void Init<TSet, TEnum, T2Enum>(
        this IServiceCollection services,
        Assembly assembly)
        where TSet : IMappingSet<TEnum, T2Enum>
        where TEnum  : Enum
        where T2Enum : Enum
    {
        if (_initialized) return;
        _initialized = true;

        var sets = assembly.GetTypes()
            .Where(t => typeof(TSet).IsAssignableFrom(t)
                        && !typeof(GraphModel).IsAssignableFrom(t)
                        && !t.IsInterface
                        && !t.IsAbstract)
            .Select(t => (TSet)Activator.CreateInstance(t)!)
            .ToList();
        
        var enum1Values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToList();
        var enum2Values = Enum.GetValues(typeof(T2Enum)).Cast<T2Enum>().ToList();

        for (int i = 0; i < enum2Values.Count; i++)
        {
            var set = sets.FirstOrDefault(a =>
                a.GetType().Name.Replace("MappingSet", "").Matches(enum2Values[i].ToString()) &&
                !a.GetType().Name.Replace("MappingSet", "").Matches(enum1Values[1].ToString()));

            if (set == null)
            {
                continue;
            }
            
            set.Register(enum1Values[0], enum2Values[i]);
        }
        
        for (int i = 0; i < enum2Values.Count; i++)
        {
            var set = sets.FirstOrDefault(a =>
                a.GetType().Name.Replace("MappingSet", "").Matches(enum2Values[i].ToString()) &&
                !a.GetType().Name.Replace("MappingSet", "").Matches(enum1Values[0].ToString()));

            if (set == null)
            {
                continue;
            }
            
            set.Register(enum1Values[1], enum2Values[i]);
        }

        MappingWarmup.Warmup(MappingRegistry.Registry);

        services.AddSingleton<IMapper>(
            new Mapper(MappingRegistry.Registry));
    }
}