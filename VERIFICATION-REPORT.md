# Benchmark package verification

Verified in the packaging environment on 2026-09-02.

## Passed

- Canonical report builder executes successfully with the repository-root path.
- Run 1 aggregate generated: 8 records.
- Run 2 aggregate generated: 16 records, including legacy Run 2 envelope handling.
- Run 3 aggregate generated: 8 records.
- Run 4 aggregate generated: 64 records.
- Run 5 aggregate generated: 32 records.
- Run 5b aggregate generated: 34 records.
- All Run 1–5b aggregates expose the same canonical metric fields.
- Benchmark matrix contains non-empty records for Runs 1–5b.
- Supply Chain aggregate remains a separate measured-plus-modeled report.
- Stale `benchmarks/docs-site` output was removed; canonical assets live under `docs-site/assets/agent-benchmark`.
- JSON parsing and schema checks passed.
- Root runner and independent-validation scripts are present.

## Environment limitation

This packaging environment does not provide Windows PowerShell, `pwsh`, or the .NET SDK, so the Windows PowerShell commands and .NET build could not be executed here. The package therefore does not claim a PowerShell/.NET execution test that was not performed.

The external validation script is included so it can be run on the intended Windows/.NET/Docker environment.
