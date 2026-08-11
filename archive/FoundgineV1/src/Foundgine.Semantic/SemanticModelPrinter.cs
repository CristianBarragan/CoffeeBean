namespace Foundgine.Semantic;

/// <summary>
/// Renders a <see cref="SemanticEntity"/> as the enumeration tree
/// Milestone 1's acceptance test describes, e.g.:
///
/// <code>
/// Customer
///  ├── identity: Id
///  ├── fields: Name
///  └── relationship: Accounts
/// </code>
///
/// This exists so "Foundgine can enumerate the domain" has one obvious,
/// testable, human-readable representation -- used by the Banking sample
/// to prove the model out loud, and by tests to pin the acceptance shape.
/// A row is only printed when the entity actually has something for it;
/// an entity with no actions simply has no "actions" row, the same way
/// the Account and Transaction entities in Milestone 1 have no "actions"
/// row despite Customer being called out for having none.
/// </summary>
public static class SemanticModelPrinter
{
    public static string Describe(SemanticEntity entity)
    {
        var rows = new List<(string Label, string Value)>
        {
            ("identity", entity.Identity.Name)
        };

        if (entity.Fields.Count > 0)
        {
            rows.Add(("fields", string.Join(", ", entity.Fields.Select(f => f.Name))));
        }

        foreach (var relationship in entity.Relationships)
        {
            rows.Add(("relationship", relationship.Name));
        }

        if (entity.Actions.Count > 0)
        {
            rows.Add(("actions", string.Join(", ", entity.Actions.Select(a => a.Name))));
        }

        var lines = new List<string> { entity.Name };

        for (var i = 0; i < rows.Count; i++)
        {
            var connector = i == rows.Count - 1 ? "└──" : "├──";
            lines.Add($" {connector} {rows[i].Label}: {rows[i].Value}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string Describe(SemanticModel model) =>
        string.Join(Environment.NewLine + Environment.NewLine, model.Entities.Select(Describe));
}
