namespace Foundgine.Security.Authority;

/// <summary>
/// Resolves durable witness identifiers into live runtime witness handles.
/// Runtime callbacks and anchor references never belong in the durable ledger.
/// </summary>
public interface IAuthorizationRecoveryWitnessResolver
{
    IReadOnlyList<AuthorizationRecoveryQuorumWitness> Resolve(IReadOnlyList<string> witnessIds);
}

public sealed class AuthorizationRecoveryReconciliationException : Exception
{
    public AuthorizationRecoveryReconciliationException(string message) : base(message) { }
}
