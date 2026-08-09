
namespace Graphgine
{
    // QueryResult.cs
    public class QueryResult<M> where M : class
    {
        public List<M> Models { get; set; } = new();
        public List<string> Cursors { get; set; } = new();
        public string? StartCursor { get; set; }
        public string? EndCursor { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public int TotalCount { get; set; }
        public int TotalPageRecords { get; set; }
    }
}