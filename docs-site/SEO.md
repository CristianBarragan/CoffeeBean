# Foundgine website SEO

The site is organized around the current product story rather than milestone history.

## Primary pages

- `/` — problem, intention, execution model and current evidence.
- `/what-is-foundgine.html` — product definition.
- `/how-it-works/` — concrete execution walkthrough.
- `/architecture/` — architecture and boundaries.
- `/ai-agents/` — agent-facing rationale.
- `/agent-benchmark/` — five-run benchmark explorer.
- `/performance/` — broader performance evidence.

## Benchmark indexing

The benchmark landing page is the canonical indexed URL. Individual run selections use `?run=N` and are intentionally not separate canonical documents. The old `/agent-benchmark/run-2/` path is a redirect to the canonical explorer.

## Redirect policy

`404.html` routes old benchmark/run URLs to the current benchmark explorer and unknown pages to the main Foundgine index. This avoids dead ends after the milestone/phase reorganization.

## Metadata

Every primary page should have a unique title, description, canonical URL and Open Graph metadata. The benchmark also exposes TechArticle JSON-LD. `robots.txt` references the XML sitemap.
