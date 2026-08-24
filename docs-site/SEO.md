# Foundgine website SEO

The site is organized around the current product story rather than milestone history.

## Primary pages

- `/` — problem, intention, execution model and current evidence.
- `/what-is-foundgine.html` — product definition.
- `/how-it-works/` — concrete execution walkthrough.
- `/architecture/` — architecture and boundaries.
- `/ai-agents/` — agent-facing rationale.
- `/agent-benchmark/` — five-run benchmark explorer.
- `/agent-benchmark/run-1/` … `/agent-benchmark/run-5b/` — individual run pages, each a standalone indexable document with a unique title, description and finding.
- `/agent-benchmark/supply-chain/` — Supply Chain end-to-end report.
- `/performance/` — broader performance evidence.

## Benchmark indexing

The benchmark landing page (`/agent-benchmark/`) is the hub and carries the broadest metadata. Each run also has its own standalone page (`/agent-benchmark/run-N/`) with unique content, a unique `<title>`/description, its own canonical URL, and TechArticle JSON-LD — these are real indexable documents, not just `?run=N` deep links into the explorer, and are included in `sitemap.xml`. The old un-hyphenated paths (`/agent-benchmark/run2/`, `/agent-benchmark/run5/`) are meta-refresh redirects to the canonical hyphenated run pages and are excluded from the sitemap.

## Redirect policy

`404.html` routes old benchmark/run URLs to the current benchmark explorer and unknown pages to the main Foundgine index. This avoids dead ends after the milestone/phase reorganization.

## Metadata

Every indexable page (7 primary pages + 6 run pages + supply-chain) has a unique title, meta description (kept under ~160 characters), self-referencing canonical URL, Open Graph tags (including `og:image`/`og:image:width`/`og:image:height` using the shared `Foundgine.webp` social banner), `twitter:card` set to `summary_large_image` with matching `twitter:image`, and schema.org JSON-LD (`TechArticle` per content page; `Organization` + `WebSite` on the homepage). `robots.txt` references the XML sitemap, and `sitemap.xml` lists every indexable URL with `lastmod`.
