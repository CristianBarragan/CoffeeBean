using Foundgine.Abstractions;

namespace Foundgine.Semantics.Resolution;

/// <summary>
/// Resolves free-form lexical tokens by first generating candidates across
/// semantic kinds and then selecting a high-scoring path constrained by the
/// frozen semantic graph. Retrieval scores order hypotheses; graph topology
/// determines whether a hypothesis is legal.
/// </summary>
public sealed class SemanticLexicalResolver
{
    private readonly SemanticContractSnapshot _contract;
    private readonly ISemanticLexicalCandidateSource _source;
    private readonly int _candidateLimit;
    private readonly int _maxBridgeHops;

    public SemanticLexicalResolver(
        SemanticContractSnapshot contract,
        ISemanticLexicalCandidateSource source,
        int candidateLimit = 20,
        int maxBridgeHops = 4)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        if (candidateLimit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(candidateLimit));
        if (maxBridgeHops is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(maxBridgeHops));
        _candidateLimit = candidateLimit;
        _maxBridgeHops = maxBridgeHops;
    }

    public SemanticLexicalResolution Resolve(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Lexical expression cannot be empty.", nameof(expression));

        var tokens = Tokenize(expression);
        if (tokens.Length == 0)
            return new(SemanticLexicalResolutionOutcome.Unresolved, [], 0, null, "No lexical tokens were found.");

        var candidateSets = GetCandidates(tokens);

        if (candidateSets.Values.Any(x => x.Count == 0))
        {
            var missing = tokens.First(x => candidateSets[x].Count == 0);
            return new(
                SemanticLexicalResolutionOutcome.Unresolved,
                [],
                0,
                null,
                $"No lexical candidate was returned for token '{missing}'.");
        }

        var roots = candidateSets[tokens[0]]
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var paths = new List<SemanticLexicalResolution>();
        foreach (var root in roots)
        {
            var state = CreateRootState(tokens[0], root);
            if (state is null)
                continue;

            Search(tokens, 1, candidateSets, state, paths);
        }

        if (paths.Count == 0)
            return new(
                SemanticLexicalResolutionOutcome.Unresolved,
                [],
                0,
                null,
                "No complete semantic path could be constructed from the lexical candidates.",
                roots);

        var ordered = paths
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.RootEntity?.Value ?? ulong.MaxValue)
            .ToArray();
        var best = ordered[0];
        var ambiguous = ordered.Length > 1 && Math.Abs(best.Confidence - ordered[1].Confidence) < 0.03d;

        return best with
        {
            Outcome = ambiguous ? SemanticLexicalResolutionOutcome.Ambiguous : SemanticLexicalResolutionOutcome.Resolved,
            RootCandidates = roots
        };
    }

    /// <summary>Returns the ranked candidate matrix used by the resolver.
    /// Each token is queried once across all semantic kinds.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SemanticLexicalCandidate>> GetCandidates(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Lexical expression cannot be empty.", nameof(expression));

        return GetCandidates(Tokenize(expression));
    }

    private IReadOnlyDictionary<string, IReadOnlyList<SemanticLexicalCandidate>> GetCandidates(
        IReadOnlyList<string> tokens)
    {
        return tokens.ToDictionary(
            token => token,
            token => (IReadOnlyList<SemanticLexicalCandidate>)_source.Retrieve(
                    new SemanticLexicalRequest(token, Limit: _candidateLimit))
                .Where(x => x.Score >= 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.CanonicalName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private void Search(
        IReadOnlyList<string> tokens,
        int index,
        IReadOnlyDictionary<string, IReadOnlyList<SemanticLexicalCandidate>> candidates,
        SearchState state,
        List<SemanticLexicalResolution> results)
    {
        if (index == tokens.Count)
        {
            var confidence = state.Steps.Count == 0
                ? 0
                : state.Steps.Select(x => x.Candidate.Score).Average() * state.GraphFactor;
            results.Add(new(
                SemanticLexicalResolutionOutcome.Resolved,
                state.Steps,
                Math.Clamp(confidence, 0, 1),
                state.RootEntity,
                "A complete lexical path was grounded against the semantic contract."));
            return;
        }

        var token = tokens[index];
        foreach (var candidate in candidates[token].Take(_candidateLimit))
        {
            foreach (var transition in ResolveTransition(state, candidate))
            {
                var next = state.Add(
                    new SemanticLexicalStep(token, candidate, transition.Factor, transition.BridgingPath),
                    transition.Factor);
                Search(tokens, index + 1, candidates, next, results);
            }
        }
    }

    private SearchState? CreateRootState(string token, SemanticLexicalCandidate candidate)
    {
        return candidate.Kind switch
        {
            SemanticLexicalCandidateKind.Entity or SemanticLexicalCandidateKind.Node
                when candidate.EntityId is not null && _contract.TryGet(candidate.EntityId.Value, out _) =>
                    new(candidate.EntityId.Value, candidate.EntityId.Value, [new SemanticLexicalStep(token, candidate, candidate.Score, [])], candidate.Score),

            SemanticLexicalCandidateKind.Relationship
                when candidate.SourceEntityId is not null && candidate.TargetEntityId is not null &&
                     _contract.TryGet(candidate.SourceEntityId.Value, out _) &&
                     _contract.TryGet(candidate.TargetEntityId.Value, out _) =>
                    new(candidate.SourceEntityId.Value, candidate.TargetEntityId.Value, [new SemanticLexicalStep(token, candidate, candidate.Score, [])], candidate.Score),

            SemanticLexicalCandidateKind.Traversal
                when candidate.SourceEntityId is not null && candidate.TargetEntityId is not null &&
                     _contract.TryGet(candidate.SourceEntityId.Value, out _) &&
                     _contract.TryGet(candidate.TargetEntityId.Value, out _) =>
                    new(candidate.SourceEntityId.Value, candidate.TargetEntityId.Value, [new SemanticLexicalStep(token, candidate, candidate.Score, [])], candidate.Score),

            SemanticLexicalCandidateKind.Field or SemanticLexicalCandidateKind.Value
                when candidate.EntityId is not null && _contract.TryGet(candidate.EntityId.Value, out _) =>
                    new(candidate.EntityId.Value, candidate.EntityId.Value, [new SemanticLexicalStep(token, candidate, candidate.Score, [])], candidate.Score),

            _ => null
        };
    }

    private IReadOnlyList<Transition> ResolveTransition(SearchState state, SemanticLexicalCandidate candidate)
    {
        var owner = candidate.Kind switch
        {
            SemanticLexicalCandidateKind.Entity or SemanticLexicalCandidateKind.Node => candidate.EntityId,
            SemanticLexicalCandidateKind.Field or SemanticLexicalCandidateKind.Value => candidate.EntityId,
            SemanticLexicalCandidateKind.Relationship or SemanticLexicalCandidateKind.Traversal => candidate.SourceEntityId,
            _ => null
        };

        if (owner is null)
            return [];

        if (owner.Value == state.CurrentEntity)
            return [new(1.0d, [])];

        var path = FindPath(state.CurrentEntity, owner.Value, _maxBridgeHops);
        if (path.Count == 0)
            return [];

        // A bridge is legal, but deliberately penalized. Direct neighbours beat
        // longer inferred paths when lexical scores are otherwise comparable.
        var factor = Math.Pow(0.90d, path.Count);
        return [new(factor, path)];
    }

    private IReadOnlyList<SemanticLexicalCandidate> FindPath(EntityId source, EntityId target, int maxHops)
    {
        if (source == target) return [];

        var queue = new Queue<(EntityId Entity, List<SemanticLexicalCandidate> Path)>();
        var visited = new HashSet<EntityId> { source };
        queue.Enqueue((source, []));

        while (queue.Count > 0)
        {
            var (entityId, path) = queue.Dequeue();
            if (path.Count >= maxHops) continue;

            var entity = _contract.Get(entityId);
            foreach (var relationship in entity.Relationships)
            {
                var targetEntity = _contract.Get(relationship.Target);
                var hop = new SemanticLexicalCandidate(
                    relationship.Name,
                    SemanticLexicalCandidateKind.Relationship,
                    relationship.Name,
                    1,
                    RelationshipId: relationship.Id,
                    SourceEntityId: entity.Id,
                    TargetEntityId: targetEntity.Id);
                var nextPath = path.Concat([hop]).ToList();
                if (targetEntity.Id == target)
                    return nextPath;
                if (visited.Add(targetEntity.Id))
                    queue.Enqueue((targetEntity.Id, nextPath));
            }
        }

        return [];
    }

    private static string[] Tokenize(string expression) =>
        expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim(',', '.', ';', ':', '?', '!', '(', ')', '[', ']', '{', '}'))
            .Where(x => x.Length > 0)
            .ToArray();

    private sealed record Transition(double Factor, IReadOnlyList<SemanticLexicalCandidate> BridgingPath);

    private sealed record SearchState(
        EntityId RootEntity,
        EntityId CurrentEntity,
        IReadOnlyList<SemanticLexicalStep> Steps,
        double GraphFactor)
    {
        public SearchState Add(SemanticLexicalStep step, double factor) =>
            new(RootEntity, ResolveCurrent(step.Candidate), Steps.Append(step).ToArray(), GraphFactor * factor);

        private static EntityId ResolveCurrent(SemanticLexicalCandidate candidate) =>
            candidate.TargetEntityId
            ?? candidate.EntityId
            ?? candidate.SourceEntityId
            ?? throw new InvalidOperationException("Lexical candidate has no semantic entity context.");
    }
}
