namespace Foundgine.Semantic;

/// <summary>
/// A named authorization or business rule attached to an entity or an
/// action -- e.g. "Refund permission", "Customer ownership",
/// "amount &lt;= configured limit" from Milestone 5's IssueRefund example.
///
/// Milestone 1 only needs this shape to exist so entities and actions have
/// somewhere to attach rules to; Milestone 5 is where a policy actually
/// gets evaluated during planning and a denied plan becomes explicit and
/// explainable. Until then, an empty policy list simply means "nothing
/// declared yet", not "no policy applies".
/// </summary>
public sealed record PolicyDescriptor(string Name, string Description);
