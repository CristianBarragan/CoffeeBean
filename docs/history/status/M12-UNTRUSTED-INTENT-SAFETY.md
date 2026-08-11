# M12 — Untrusted Intent Safety

M12 proves the AI/LLM property without adding an LLM dependency.

## Boundary

```text
LLM / agent / external client
            ↓
       JSON intent
            ↓
   JsonReadIntentAdapter
            ↓
       ReadIntent
            ↓
   ReadIntentCompiler
            ↓
    SemanticRequestResolver
            ↓
      SemanticAuthorizer
            ↓
          Planner
            ↓
        Provider
```

The producer is untrusted. It can propose arbitrary semantic names and values, but it cannot execute SQL or bypass resolution and authorization.

## Safety properties

### 1. Unknown concepts fail closed

Unknown entities, fields, and relationships are rejected by semantic compilation/resolution before planning.

### 2. Authorization is downstream of parsing

The JSON adapter never decides whether a field or relationship is allowed. `SemanticAuthorizer` remains the single authorization boundary.

### 3. Authorization cannot be bypassed by JSON

A JSON request containing a denied field is converted into normal semantic intent and then filtered by the existing authorization policy. The denied field does not reach SQL planning.

### 4. Root denial is fatal

A denied root entity raises `SemanticAuthorizationException`. The request does not continue to planning.

### 5. Parser resource bounds

The JSON adapter bounds:

- selection depth
- selection count
- filter depth
- filter node count
- nested JSON value depth

These are protocol-boundary protections against pathological generated input. They are not authorization rules.

## What M12 does not claim

M12 does not claim that an LLM is trustworthy. It proves the opposite design assumption: **the producer may be untrusted because Foundgine validates and authorizes the resulting semantic intent before execution.**

No LLM SDK, prompt framework, MCP server, or model-specific dependency is introduced into Foundgine.
