# Retrieval Strategies (Fuzzy / Full-Text / Search / Graph / Vector)

Files: `Tests/Retrieval/*.cs`. Provider under test:
`Foundgine.Providers.Storage.Sql.Retrieval.PostgresRetrievalCandidateSource`
(see also `src/Foundgine.Providers.Storage.Sql/README.md`).

## The concept: retrieval feeds grounding, grounding feeds planning

Where `03-Ambiguity-And-Grounding.md` covers *what happens* once candidate
interpretations exist, this file covers *where those candidates come from*
for a real, non-toy schema: `PostgresRetrievalCandidateSource` implements
several distinct `RetrievalStrategy` values, each backed by a different
PostgreSQL capability, each tested against fixtures shaped like this
sample's actual `Supplier`/`Product` tables.

## The five strategies, what each needs, and how to opt in

| Strategy | Backed by | Opt-in required | Test file |
|---|---|---|---|
| `Fuzzy` | `pg_trgm` (trigram similarity) | `FOUNDGINE_POSTGRES_CONNECTION` only — enabled by default on any Postgres with the extension | `SupplyChainFuzzyAndFullTextRetrievalTests.cs` |
| `FullText` | Native Postgres full-text search | `FOUNDGINE_POSTGRES_CONNECTION` only | `SupplyChainFuzzyAndFullTextRetrievalTests.cs` |
| `Search` | pg_search (ParadeDB BM25) | `FOUNDGINE_POSTGRES_CONNECTION` **and** `FOUNDGINE_POSTGRES_PGSEARCH=1` | `SupplyChainSearchRetrievalTests.cs` |
| `GraphSimilarity` | Apache AGE | `FOUNDGINE_POSTGRES_CONNECTION` **and** `FOUNDGINE_POSTGRES_AGE=1` | `SupplyChainGraphSimilarityRetrievalTests.cs` |
| `Vector` | *(reserved for a future pgvector provider)* | n/a — always short-circuits | `SupplyChainRetrievalCapabilityTests.cs` |
| `Relational` | *(documented no-op)* | n/a | `SupplyChainRetrievalCapabilityTests.cs` |

**Why two strategies need a second, explicit opt-in on top of the connection
string:** pg_search and Apache AGE are not installed on a vanilla PostgreSQL
image — unlike `pg_trgm` and full-text search, which ship with core
Postgres. Gating them behind their own environment variables means CI and a
typical local Postgres stay green (those tests are *skipped*, not *failed*)
while anyone who has actually installed the extension gets full coverage.
This is the same "opt-in, not opt-out" philosophy the claims validator uses
for unrecognized claim keys (see `01-Claims-And-Authorization.md`) — absence
of a capability degrades gracefully instead of failing loud in an
environment that never claimed to support it.

## Fuzzy vs. full-text: two different questions, not two strengths of the same one

It's tempting to think of `Fuzzy` and `FullText` as "loose" vs. "strict"
matching. They're actually answering different questions:
- **Fuzzy** (`pg_trgm`): "what's *character-level similar* to this string?"
  — this is what catches a misspelled supplier name
  (`Fuzzy_retrieval_matches_a_misspelled_supplier_name`), because trigram
  similarity doesn't care about word boundaries or meaning.
- **FullText**: "what documents contain these *words* (after stemming/
  stopword removal)?" — this is a token/relevance match, not a
  character-distance match, and won't catch a misspelling the way Fuzzy does.

## `Search` (BM25) — why it's a separate strategy from `FullText`

Native Postgres full-text search and ParadeDB's pg_search both operate over
text, but BM25 ranking (term frequency, inverse document frequency, field-
length normalization) produces materially different relevance ordering for
free-text queries like `"metal fabrication"` against a `Supplier.Name`
field — `SupplyChainSearchRetrievalTests.cs`'s test name
(`Search_retrieval_ranks_suppliers_by_bm25_relevance`) is explicit that
ranking quality, not just match/no-match, is what this strategy is tested
for.

## `GraphSimilarity` — a genuinely separate index, not a mirror of foreign keys

The doc comment on `SupplyChainGraphSimilarityRetrievalTests.cs` makes a
subtle point worth restating: the AGE graph models "two suppliers are
neighbor-similar when they both connect to the same purchase-order vertex"
— **even though**, relationally, a single purchase order has exactly one
owning supplier (a real foreign key, one-to-many). The AGE graph is a
separate, purpose-built retrieval index shaped for a *co-sourcing/risk
similarity* question ("which suppliers are structurally similar to this
one?"), not a graph-shaped copy of the relational schema. This is worth
internalizing if you're adding a new graph retrieval case: the question is
"what similarity structure does this business question need," not "how do I
represent my foreign keys as edges."

## `Vector` and `Relational` — the two strategies that never touch Postgres

`SupplyChainRetrievalCapabilityTests.cs` is the one file in this set that
needs **no real database at all** — it builds a syntactically valid but
unreachable connection string (`Host=127.0.0.1;Port=1;...;Timeout=1`) and
asserts that every strategy either:
- short-circuits before issuing any command (an opt-in gate that's off, a
  request-shape validation failure, or `Vector` being reserved), or
- is a documented no-op (`Relational`).

This is provider-*wiring* coverage, deliberately separate from the
provider-*behavior* coverage in the other four files: it proves the gating
logic itself is correct (a disabled strategy never reaches PostgreSQL to
find out it's disabled) without needing any infrastructure to do so.

---
Previous: [`03-Ambiguity-And-Grounding.md`](./03-Ambiguity-And-Grounding.md) · Next: [`05-Adversarial-Security-Testing.md`](./05-Adversarial-Security-Testing.md)
