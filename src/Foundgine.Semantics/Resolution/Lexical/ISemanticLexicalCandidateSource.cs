namespace Foundgine.Semantics.Resolution;

/// <summary>Retrieves lexical candidates across all semantic kinds.</summary>
public interface ISemanticLexicalCandidateSource
{
    IReadOnlyList<SemanticLexicalCandidate> Retrieve(SemanticLexicalRequest request);
}
