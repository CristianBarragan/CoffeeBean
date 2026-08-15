# PostgreSQL ord_map / RETURNING audit

This stage audits the concrete PostgreSQL `ord_map` / `RETURNING` path
before changing its SQL generation.

The central correctness question is whether generated values can be mapped
back to logical mutation operations without relying on incidental PostgreSQL
row ordering.

No semantic dependency model is changed here.

Next implementation work should modify the concrete SQL compiler only where
the audit identifies an actual violation of these invariants.
