# Foundgine website

This directory contains the static public website for the current Foundgine release line (`1.1.9`, .NET 9).

## Public story

The site is intentionally organized around the product rather than development milestones:

1. **What it is** — the semantic execution problem and model.
2. **Getting started** — the canonical Supply Chain application.
3. **How it works** — request lifecycle with representative payloads.
4. **Architecture** — semantic, planning, execution and provider boundaries.
5. **AI agents** — host-owned authority and controlled model integration.
6. **Security** — authorization invariants and execution gates.
7. **Samples** — canonical, semantic and PenTest applications.
8. **Packages** — all source packages and their responsibilities.
9. **Evidence** — controlled agent benchmarks, Supply Chain E2E and scoped performance evidence.

## Content policy

The public website should describe the current architecture. Do not add milestone/phase notes, obsolete release snapshots, abandoned design alternatives, or historical implementation details to the public navigation.

Historical development material belongs in the repository history or changelog, not in the current product narrative.

## Benchmark assets

Only published aggregate measurements and the Supply Chain report are retained as website data. Large per-request captures and obsolete benchmark experiments are not part of the public site.

Benchmark claims must distinguish measured values from estimated context metrics and must state the workload/concurrency scope.

## Editing

The website is static HTML with shared CSS and small JavaScript components. The Markdown files beside major pages provide concise source/context versions for documentation and LLM ingestion; the served HTML remains the public rendering source.

When adding a page:

1. use the existing site shell and navigation;
2. describe the current implementation, not historical milestones;
3. link to the relevant source README/sample when deeper detail is needed;
4. add the page to `sitemap.xml` and `llms.txt` when it is public/indexable;
5. verify all relative links before committing.
