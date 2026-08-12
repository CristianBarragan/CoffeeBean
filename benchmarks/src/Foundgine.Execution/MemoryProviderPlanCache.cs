using System.Collections.Concurrent;

namespace Foundgine.Execution;

/// <summary>
/// Small bounded in-memory provider-plan cache. The cache is intentionally
/// process-local and per Foundgine engine registration. A cache hit never skips
/// semantic resolution or authorization; it only skips provider compilation.
/// </summary>
public sealed class MemoryProviderPlanCache : IProviderPlanCache
{
    private readonly ConcurrentDictionary<string, ProviderPlan> _plans = new(StringComparer.Ordinal);
    private readonly int _capacity;

    public MemoryProviderPlanCache(int capacity = 256)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
    }

    public bool TryGet(string key, out ProviderPlan plan) =>
        _plans.TryGetValue(key, out plan!);

    public void Set(string key, ProviderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(plan);

        _plans[key] = plan;

        if (_plans.Count <= _capacity)
            return;

        // This cache is deliberately simple. Eviction is best-effort rather than
        // LRU; correctness does not depend on eviction order.
        foreach (var candidate in _plans.Keys)
        {
            if (_plans.Count <= _capacity)
                break;

            _plans.TryRemove(candidate, out _);
        }
    }
}
