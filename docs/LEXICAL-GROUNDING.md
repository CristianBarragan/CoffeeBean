# Lexical grounding

Foundgine can resolve free-form language without requiring complete sentences or
predefined query templates. The semantic contract remains authoritative; an
approximate retrieval provider only proposes candidates.

## Canonical flow

```text
Natural language
      ↓
Tokenization
      ↓
Candidate retrieval (for every token × semantic kind)
      ↓
Ranked lexical candidates
      ↓
Highest-scoring root candidate
      ↓
Semantic-contract validation
      ↓
Neighbour-constrained graph walk
      ↓
Backtracking when a candidate cannot form a valid path
      ↓
Canonical semantic interpretation
      ↓
Authorization
      ↓
Planning / execution
```

For each token, the retrieval boundary can consider:

- Entity
- Node
- Relationship
- Traversal
- Field
- Value
- Operation

The highest retrieval score is the **first hypothesis**, not truth. A lower
scoring candidate can win if it forms a valid semantic path while the higher
scoring candidate cannot.

## Elasticsearch

`Foundgine.Elasticsearch` is optional. It projects a frozen
`SemanticContractSnapshot` through `SemanticLexiconProjection` and retrieves
ranked candidates through Elasticsearch BM25/fuzzy matching.

The index contains structural documents for entities, nodes, fields and
relationships. Domain values such as `Nike` and `Shoes` are separate value
documents because they are data vocabulary, not structural declarations.

Elasticsearch relevance `_score` is never treated as a probability. Foundgine
combines retrieval relevance with semantic graph compatibility and path
continuity before accepting an interpretation.

## PostgreSQL (pgvector)

`Foundgine.Postgres.Vector` is optional and implements the same
`ISemanticLexicalCandidateSource` boundary as `Foundgine.Elasticsearch`. It
projects a frozen `SemanticContractSnapshot` through the same
`SemanticLexiconProjection`, embeds each entry with a caller-supplied
`ISemanticEmbeddingGenerator`, and stores the vectors in a PostgreSQL table
via the `pgvector` extension. Candidate retrieval ranks by cosine (or L2, or
inner-product) distance instead of BM25.

pgvector distance is converted to a bounded relevance score the same way
Elasticsearch's `_score` is used: as a retrieval hypothesis, never a
probability and never an authorization decision. The projected table is not
Foundgine's semantic memory — it is a derived, disposable retrieval index.
Aliases/synonyms, graph neighbors, and embeddings are three separate
concerns; only the frozen semantic contract is authoritative for the first
two.

Because both providers implement the same interface, a deployment can run
either one, or combine candidates from both before handing them to
`SemanticLexicalResolver`.

## Example

Given:

```text
Customer
  └─ Orders → SalesOrder
                └─ Lines → SalesOrderLine
                              └─ Product → CatalogProduct
                                              └─ Category → Category
```

and a lexical expression:

```text
bought nike shoes
```

Elasticsearch can return candidates such as:

```text
bought → Orders                    0.98
nike   → CatalogProduct.Name=...   0.99
shoes  → Category.Name=...         0.97
```

Foundgine then validates the path:

```text
Customer
  → Orders
  → SalesOrder
  → Lines
  → SalesOrderLine
  → Product
  → CatalogProduct
  → Category
  → Category.Name
```

The database is queried only after this semantic interpretation has been
resolved and authorized. It does not decide what the words mean.
