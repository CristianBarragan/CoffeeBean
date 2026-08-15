# SQL correlation contract — Concrete PostgreSQL Compiler Audit

Compiler: `src/Foundgine.Sql/Mutation/PostgresBatchedMutationCompiler.cs`

Relevant SQL-generation references:

- `15: /// expanded with PostgreSQL unnest(array parameters). Reference-valued fields use`
- `16: /// the source group's 1-based unnest ordinal and an ord-map CTE, so generated`
- `19: /// Update/Delete operations remain one CTE each, but are still folded into the`
- `161: // row_to_json(record) preserving the synthetic CTE column when the`
- `162: // record shape is assembled through UNION/CTE projections.`
- `178: .Append(meta.IsOrdinalAddressable && meta.OrdMapCteName is not null`
- `179: ? meta.OrdMapCteName`
- `180: : meta.ResultCteName)`
- `438: .Append(" AS (\n  SELECT * FROM unnest(")`
- `446: // Resolve reference columns once in a sibling CTE. This is also used to`
- `496: sql.Append("\n  JOIN ").Append(source.OrdMapCteName)`
- `566: sql.Append("\n  RETURNING ")`
- `593: // result set reads from this CTE so __ord survives row_to_json().`
- `648: if (isDelete && !rewritten.Contains(" RETURNING ", StringComparison.OrdinalIgnoreCase))`
- `649: rewritten += " RETURNING 1 AS \"__affected\"";`
- `668: var resultCte = $"g{group.GroupId}_op";`
- `674: .Append(resultCte)`
- `690: resultCte,`
- `754: throw new InvalidOperationException($"Return column '{columnName}' was not projected.");`
- `929: string ResultCteName,`
- `930: string? OrdMapCteName,`

This stage intentionally does not alter the concrete compiler until the exact SQL builder and mutation-plan APIs are established from the uploaded source. The audit prevents an invented `RETURNING ordinal` implementation from being applied to the wrong abstraction.
