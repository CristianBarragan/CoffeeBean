# Security

Foundgine treats external intent as untrusted input.

The important rule is:

```text
Input → Parse → Resolve → Authorize → Plan → Execute
```

Do not allow an adapter to bypass resolution or authorization.

SQL values are parameterized by the SQL provider. External GraphQL or JSON names do not become SQL identifiers or executable provider operations without going through the semantic and planning layers.

For AI-generated intent, apply normal application authentication, authorization, rate limits, validation, and approval controls around the Foundgine boundary.
