using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using Newtonsoft.Json;

namespace CoffeeBeanery.Cache;

public static class CacheHelper
{
    public static string GetKey(Dictionary<string, string> dictionary)
    {
        return string.Join("~", dictionary.Keys);
    }

    public static string GetKey(SqlCompilationContext context)
    {
        return JsonConvert.SerializeObject(context);
    }

    public static M GetJson<M>(string sqlStructure) where M : class
    {
        return JsonConvert.DeserializeObject<M>(sqlStructure);
    }
}