using Foundgine.Metadata;

namespace Foundgine.Semantic;

/// <summary>
/// A plain (non-relationship) attribute of a <see cref="SemanticEntity"/>,
/// e.g. Customer.Name or Account.Balance. Deliberately protocol-neutral:
/// no column, no storage detail, no GraphQL type -- just what a domain
/// author, or an agent reasoning about the domain, would call the field
/// and what CLR type its value has.
/// </summary>
public sealed record SemanticField(FieldId Id, string Name, Type ClrType);
