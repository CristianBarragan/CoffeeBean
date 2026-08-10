# Getting Started FAQ

## Does Foundgine require an LLM?

No.

The current core accepts structured intent.

An LLM is one possible producer of that intent.

## Does it require GraphQL?

No.

The active Banking sample has no GraphQL dependency.

## Does it require PostgreSQL?

No.

The canonical proof uses in-memory SQLite.

## Is it an ORM?

No.

Foundgine plans execution; it does not provide object tracking or attempt to replace EF Core.

## Is it production-ready?

No.

The execution substrate is real and tested, but the AI-native semantic/action/policy lifecycle is still being developed.

## Why is the semantic model hand-authored?

Because the semantic model is currently being proven before investing in a compiler.

The long-term direction is to infer as much as possible from existing application metadata.

## Why not parse natural language inside Foundgine?

Because language understanding is better owned by an LLM/parser.

Foundgine should receive structured intent and enforce domain semantics deterministically.
