using System.Collections.Generic;

namespace Graphgine.Execution.Ordering;

public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>
/// One sort term: Path is the navigation path to the field (e.g.
/// ["customer", "firstNaming"] for `{ customer: { firstNaming: ASC } }`,
/// or just ["accountNumber"] for a root-level field).
///
/// Deliberately split out of OrderCompiler.cs (which stays in
/// Graphgine.HotChocolate, since it parses HotChocolate's IValueNode) --
/// OrderSqlWriter (Runtime) needs this pure data shape without pulling in
/// a HotChocolate reference, the same reasoning AdapterLookup was split
/// out of HotChocolateAdapter for.
/// </summary>
public sealed record OrderTerm(
    IReadOnlyList<string> Path,
    SortDirection Direction);
