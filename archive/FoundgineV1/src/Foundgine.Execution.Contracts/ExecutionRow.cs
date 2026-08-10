using Foundgine.Metadata;

namespace Foundgine.Execution.Contracts;

/// <summary>
/// One scanned occurrence of an entity within a single <see cref="ExecutionRow"/>.
///
/// <see cref="EntityId"/> alone identifies WHAT was scanned (e.g. "Employee");
/// <see cref="OccurrenceIndex"/> identifies WHICH scan of it, in the same
/// left-to-right order the SQL SELECT list scanned it — 0 for the first
/// occurrence of that entity in the plan, 1 for the second, and so on. For
/// a plan that never scans the same entity twice (the common case), every
/// occurrence's index is 0. For a repeated-entity/self-join plan (e.g.
/// <c>Employee -> Manager -> Manager</c>, all three the same
/// <see cref="EntityId"/>), this is what distinguishes "the employee"
/// (index 0) from "their manager" (index 1) from "their manager's
/// manager" (index 2) — without it, three distinct scans of the same
/// entity would have nowhere to live except one shared, overwritten slot.
/// See <see cref="ExecutionRow"/>'s remarks for how this replaced exactly
/// that bug.
/// </summary>
public sealed record EntityOccurrence(
    EntityId EntityId,
    int OccurrenceIndex,
    object?[] Values
);

/// <summary>
/// A single streamed row produced by an execution provider: every scanned
/// entity <em>occurrence</em>, in the same left-to-right order the SQL
/// SELECT list scanned them — not just entity identity.
///
/// This used to be <c>IReadOnlyDictionary&lt;ushort, object?[]&gt;</c>,
/// keyed by <see cref="EntityId"/> alone. That worked for every plan that
/// scans each entity at most once, but it has no way to represent a
/// repeated-entity/self-join plan (e.g. <c>Employee -> Manager -></c>
/// <c>Manager</c>): three separate <c>Employee</c> scans would all
/// collapse onto the same dictionary key, and
/// <see cref="Foundgine.Providers.SqlExecutionProvider"/>'s row reader
/// would silently let the last one scanned overwrite the earlier ones'
/// values. <see cref="RepeatedEntityEndToEndTests"/> found exactly that.
///
/// <see cref="Occurrences"/> fixes it by keying on (entity, occurrence)
/// instead of entity alone — see <see cref="EntityOccurrence"/>.
/// <see cref="Single"/> and <see cref="All"/> below exist so the common,
/// non-repeated case doesn't need to think about occurrence indices at
/// all, while still making it impossible to silently read the wrong
/// occurrence's data when an entity does repeat.
/// </summary>
public sealed record ExecutionRow(
    IReadOnlyList<EntityOccurrence> Occurrences
)
{
    /// <summary>
    /// The one occurrence of <paramref name="entityId"/> in this row.
    /// Throws if it was never scanned, and — deliberately — also throws if
    /// it was scanned more than once, rather than silently returning just
    /// one of several occurrences. A repeated-entity plan (self-join, or
    /// the same entity reached down two different branches) must use
    /// <see cref="All"/> or index <see cref="Occurrences"/> directly by
    /// <see cref="EntityOccurrence.OccurrenceIndex"/> instead: there is no
    /// single "the" occurrence to hand back.
    /// </summary>
    public object?[] Single(EntityId entityId)
    {
        object?[]? found = null;
        var count = 0;

        foreach (var occurrence in Occurrences)
        {
            if (occurrence.EntityId != entityId)
                continue;

            found = occurrence.Values;
            count++;
        }

        return count switch
        {
            0 => throw new KeyNotFoundException(
                $"Entity {entityId.Value} was not scanned in this row."),
            1 => found!,
            _ => throw new InvalidOperationException(
                $"Entity {entityId.Value} was scanned {count} times in this row (a " +
                $"repeated-entity/self-join plan). {nameof(Single)} only works when an " +
                $"entity is scanned at most once — use {nameof(All)}({nameof(entityId)}) " +
                $"or index {nameof(Occurrences)} by {nameof(EntityOccurrence.OccurrenceIndex)} " +
                "to read each occurrence independently instead."),
        };
    }

    /// <summary>
    /// Every occurrence of <paramref name="entityId"/> in this row, in
    /// scan order — index 0 is the first occurrence, index 1 the second,
    /// and so on, matching each <see cref="EntityOccurrence.OccurrenceIndex"/>.
    /// Empty if the entity was never scanned.
    /// </summary>
    public IEnumerable<object?[]> All(EntityId entityId) =>
        Occurrences.Where(o => o.EntityId == entityId).Select(o => o.Values);
}