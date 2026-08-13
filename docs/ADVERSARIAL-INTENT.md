# Adversarial Intent Boundary

Foundgine treats agent-produced intent as **untrusted input**.

The agent can describe what it wants, but it cannot choose the physical execution mechanism or bypass semantic validation and authorization.

```text
untrusted agent input
        |
        v
 JSON / intent adapter
        |
        v
 semantic-name resolution
        |
        v
 authorization
        |
        v
 execution planning
        |
        v
 provider
```

## What the boundary rejects

The repository has regression tests for the following classes of hostile input:

- unknown entities;
- unknown fields;
- unknown relationships;
- fields pretending to be traversals by supplying children;
- unsupported filter kinds such as `rawSql`;
- excessive selection/filter/value depth;
- excessive selection/filter counts;
- unauthorized entities and fields.

## Values are data, not executable instructions

A value such as:

```text
Alice' OR 1=1 --
```

remains a value in the semantic model. The SQL provider emits a parameter rather than concatenating the value into SQL text.

This is an important distinction for agent systems: an LLM can produce malicious-looking strings, but those strings do not become provider instructions merely because they appear in an intent payload.

## The trust boundary

The adapter is responsible for turning an external representation into a deliberately small `ReadIntent`. It does **not** authorize, plan, or execute it.

The semantic compiler then resolves names against the known model. Authorization remains a separate stage, and providers receive the resulting semantic execution plan rather than the original external payload.

Therefore the intended security property is:

> **Untrusted intent can request capabilities; it cannot define capabilities.**

## What this does not claim

These tests are not a complete security audit. They prove repository-level invariants around the current intent representation and SQL parameterization. Production deployments still need ordinary controls such as authentication, transport security, resource limits, database permissions, logging, and dependency/security scanning.
