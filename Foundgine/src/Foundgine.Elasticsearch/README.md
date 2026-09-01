# Foundgine.Elasticsearch

Optional Elasticsearch integration for Foundgine lexical grounding.

The package has two responsibilities:

- `SemanticLexiconIndexClient` projects a frozen Foundgine semantic contract into an Elasticsearch index.
- `ElasticsearchSemanticLexicalCandidateSource` retrieves ranked lexical candidates for every token across semantic kinds.

Elasticsearch is only a candidate generator. Foundgine's semantic contract remains authoritative for graph compatibility, authorization, planning, and execution.

The index should also contain domain-value documents (for example `Nike` and `Shoes`) when those values are expected to be resolved from data rather than from schema declarations. Domain values are intentionally not invented by the structural semantic model.
