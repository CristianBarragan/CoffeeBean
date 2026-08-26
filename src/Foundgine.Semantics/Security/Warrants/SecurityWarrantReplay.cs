using System.Collections.Concurrent;

namespace Foundgine.Semantics.Security.Warrants;

/// <summary>Single-use replay protection for a signed warrant nonce. Implementations must provide atomic consume semantics for their deployment scope.</summary>
public interface ISecurityWarrantReplayStore
{
    bool TryConsume(string warrantId, string nonce);
}

/// <summary>Process-local replay protection. Do not use this implementation as the sole replay boundary across multiple application instances.</summary>
public sealed class MemorySecurityWarrantReplayStore : ISecurityWarrantReplayStore
{
    private readonly ConcurrentDictionary<string, byte> _used = new(StringComparer.Ordinal);

    public bool TryConsume(string warrantId, string nonce) =>
        _used.TryAdd(warrantId + "\u001f" + nonce, 0);
}

public static class SecurityWarrantReplayGuard
{
    public static void Consume(
        SecurityWarrant warrant,
        ISecurityWarrantReplayStore store,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(warrant);
        ArgumentNullException.ThrowIfNull(store);
        if (!warrant.IsTimeValid(now))
            throw new InvalidOperationException("Security warrant is expired or not yet valid.");
        if (!store.TryConsume(warrant.Id, warrant.Nonce))
            throw new InvalidOperationException("Security warrant nonce has already been consumed.");
    }
}
