> Source content for [`what-is-foundgine.html`](what-is-foundgine.html), the page actually served on the site. Edit this file, then regenerate the HTML page and `llms-full.md`.

# What Is Foundgine?

**Foundgine is a semantic execution platform for .NET that turns structured intent into authorized execution plans.**

## The problem

Applications increasingly have multiple callers:

- APIs
- GraphQL
- automation
- internal services
- AI agents

Each caller can otherwise grow its own rules for validation, authorization, query construction, and data access.

That makes the application harder to reason about and creates multiple execution paths.

## The Foundgine model

Foundgine introduces a semantic boundary:

```text
Caller
  ↓
Intent
  ↓
Semantic Model
  ↓
Authorization
  ↓
Execution Plan
  ↓
Provider
  ↓
Result
```

The caller expresses an operation. The application defines the semantic capabilities and authorization rules. Foundgine builds the execution plan and the provider performs it.

## Why semantic execution?

A persistence model describes how data is stored.

A semantic model describes what an application is willing to expose and operate on.

Those models do not have to be identical.

For example, a persistence model might contain:

```text
Customer
 ├── Id
 ├── TenantId
 ├── Name
 ├── InternalRiskScore
 └── Accounts
```

An application-facing semantic model might expose:

```text
Customer
 ├── id
 ├── name
 └── accounts
      └── balance
```

The semantic surface can therefore be smaller, safer, and more purposeful than the physical model.

## Why this matters for AI

An AI agent is good at producing intent. It should not be trusted with unrestricted database authority.

A safer architecture is:

```text
AI
 ↓
structured intent
 ↓
Foundgine
 ├── resolve
 ├── validate
 ├── authorize
 ├── plan
 └── execute
 ↓
database
```

The application remains the authority over what the agent can do.

## Core concepts

Foundgine's public mental model can remain simple:

**Model → Request → Authorize → Plan → Execute → Result**

The deeper architecture adds metadata, expression trees, relationship traversal, rewriting, optimization, cost estimation, provider capabilities, and execution evidence.

## Where Foundgine fits

Foundgine can sit underneath interfaces rather than replacing them:

```text
REST/API ──────┐
GraphQL ───────┤
Automation ────┤
AI Agent ──────┤
               ▼
           Foundgine
               ▼
        SQL / InMemory / ...
```

This lets the interface and the execution model evolve independently.
