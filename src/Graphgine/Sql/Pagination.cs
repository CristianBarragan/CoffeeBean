
namespace Graphgine.Sql
{
    public class Pagination
    {
        public string? After { get; set; }
        public string? Before { get; set; }
        public int? First { get; set; }
        public int? Last { get; set; }
        public int PageSize { get; set; }

        public string? StartCursor { get; set; }
        public string? EndCursor { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }

        public TotalRecordCount TotalRecordCount { get; set; } = new();
        public TotalPageRecords TotalPageRecords { get; set; } = new();
    }

    public class TotalRecordCount
    {
        public int RecordCount { get; set; }
    }

    public class TotalPageRecords
    {
        public int PageRecords { get; set; }
    }
}