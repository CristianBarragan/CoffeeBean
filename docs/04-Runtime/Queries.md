# Queries

## QueryIntent

`Foundgine.Planning.QueryIntent` is a structured logical request.

It can express:

- root entity;
- traversal;
- filters;
- sorting;
- paging.

## QueryPlanner

`QueryPlanner` resolves a query intent against:

```text
MetadataRegistry
JoinGraph
```

and produces a provider-neutral `QueryPlan`.

## QueryPlan

The logical plan can contain nodes such as:

```text
Scan
Join
Composite
Projection
Materialize
GraphEdge
```

## Provider compilation

The SQL provider translates the logical plan into provider nodes and SQL.

This keeps SQL syntax out of the planner.
