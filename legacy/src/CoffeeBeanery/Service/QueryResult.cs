
namespace CoffeeBeanery.Service
{
    // QueryResult.cs
    public class QueryResult<M> where M : class
    {
        public List<M> Models { get; set; } = new();
        public int? StartCursor { get; set; }
        public int? EndCursor { get; set; }
        public int TotalCount { get; set; }
        public int TotalPageRecords { get; set; }
    }
}