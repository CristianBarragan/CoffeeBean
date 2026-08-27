using System.Security.Cryptography;
using System.Text;

namespace Foundgine.Semantics;

/// <summary>
/// Stable semantic version identifiers carried across discovery, planning,
/// approval and execution. The semantic model version is derived from the
/// canonical model topology so a changed model cannot silently reuse an old
/// approval.
/// </summary>
public sealed record SemanticVersionSet(
    string SemanticModelVersion,
    int CapabilityContractVersion,
    int CapabilityVersion,
    int IntentVersion,
    int PlanVersion)
{
    public const int CurrentCapabilityContractVersion = 1;
    public const int CurrentCapabilityVersion = 1;
    public const int CurrentIntentVersion = 1;
    public const int CurrentPlanVersion = 1;

    public static SemanticVersionSet For(SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new SemanticVersionSet(
            ComputeModelVersion(model),
            CurrentCapabilityContractVersion,
            CurrentCapabilityVersion,
            CurrentIntentVersion,
            CurrentPlanVersion);
    }

    private static string ComputeModelVersion(SemanticModel model)
    {
        var builder = new StringBuilder(1024);
        foreach (var entity in model.Entities.OrderBy(x => x.Id.Value))
        {
            builder.Append("entity|").Append(entity.Id.Value).Append('|').Append(entity.Name).Append(';');
            builder.Append("identity|").Append(entity.Identity.ToString()).Append(';');

            foreach (var field in entity.Fields.OrderBy(x => x.Id.Value))
                builder.Append("field|").Append(field.Id.Value).Append('|').Append(field.Name).Append('|').Append(field.EffectiveSemanticType).Append('|').Append(field.Capabilities).Append('|').Append(field.IsNullable).Append(';');

            foreach (var relationship in entity.Relationships.OrderBy(x => x.Id.Value))
                builder.Append("relationship|").Append(relationship.Id.Value).Append('|').Append(relationship.Name).Append('|').Append(relationship.Target.Value).Append('|').Append(relationship.Cardinality).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
