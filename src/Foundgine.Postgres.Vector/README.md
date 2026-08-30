# Foundgine.Postgres.Vector

pgvector-backed lexical candidate retrieval for Foundgine's lexical grounding
pipeline (see [`docs/LEXICAL-GROUNDING.md`](../../docs/LEXICAL-GROUNDING.md)).

## What this package is

`PgVectorSemanticLexicalCandidateSource` implements
`ISemanticLexicalCandidateSource` using PostgreSQL + `pgvector` for
approximate nearest-neighbor search over embeddings of the projected semantic
lexicon.

It is one interchangeable implementation of the same provider-neutral
boundary that `Foundgine.Elasticsearch` implements. Foundgine's semantic
layer has no compile-time or runtime dependency on either.

## What this package is *not*

It is not Foundgine's "semantic memory" and it does not become the authority
for schema topology. The table this package indexes into is a **derived
retrieval projection** of a frozen `SemanticContractSnapshot` — the same
snapshot `Foundgine.Elasticsearch` projects. Three concerns stay separate:

| Concern                              | Owner                                   |
|---------------------------------------|------------------------------------------|
| Canonical names, aliases, descriptions | Semantic contract (source of truth)      |
| Graph topology / neighbor validity     | Semantic contract + graph walk           |
| Ranked candidate retrieval             | This package (or Elasticsearch, or both) |

A high vector-similarity score is a hypothesis, never an authorization. The
resolver still validates every candidate against the frozen contract before
accepting a semantic path — see `SemanticLexicalResolver` in
`Foundgine.Semantics`.

## Usage

```csharp
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
await using var dataSource = dataSourceBuilder.Build();

// Bring your own embedding model — Foundgine does not ship one.
ISemanticEmbeddingGenerator embeddings = new MyEmbeddingGenerator();

var options = new PgVectorOptions(
    TableName: "foundgine_semantic_lexicon",
    Dimensions: 1536,
    Distance: PgVectorDistance.Cosine);

// One-time / on contract change: project and index the frozen contract.
var indexClient = new PgVectorSemanticLexiconIndexClient(dataSource, embeddings, options);
await indexClient.IndexContractAsync(contractSnapshot);

// Register as a lexical candidate source for the resolver.
ISemanticLexicalCandidateSource candidateSource =
    new PgVectorSemanticLexicalCandidateSource(dataSource, embeddings, options);
```

## Swapping or combining providers

`ISemanticLexicalCandidateSource` is the only contract the resolver depends
on, so a caller can combine candidates from more than one provider (for
example, pgvector for semantic recall and Elasticsearch/BM25 for exact lexical
matches) by merging their `Retrieve` results before handing them to
`SemanticLexicalResolver`. Neither provider needs to know the other exists.

## Bring your own embedding model

`ISemanticEmbeddingGenerator` (in `Foundgine.Semantics`) is the only
embedding dependency this package has. It is intentionally unopinionated
about the model or vendor — implement it against whatever embedding API your
deployment already uses.
