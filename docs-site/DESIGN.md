# Foundgine docs — design system

Quick reference so new pages stay consistent with the rest of the site. Full source of truth is `assets/style.css`.

## Tokens

| Role | Light | Dark | Used for |
|---|---|---|---|
| `--bg` | `#EEF1F6` | `#0E1420` | page background |
| `--surface` | `#FFFFFF` | `#141B2B` | cards, tables, rail, diagrams |
| `--ink` | `#121826` | `#E7EAF2` | primary text |
| `--muted` | `#5B6577` | `#93A0B4` | secondary text, labels |
| `--line` | `#D7DCE6` | `#29334A` | borders, connectors |
| `--signal` | `#2557D6` | `#6FA1FF` | authorized / primary path |
| `--gate` | `#B8792C` | `#E3A45C` | boundary / denied / caution |

Dark variant applies automatically via `prefers-color-scheme: dark` — no toggle needed.

## Type

- **Display** (`h1`–`h3`, brand, eyebrows): Space Grotesk
- **Body**: Source Serif 4 — chosen for long-form reading comfort
- **Mono** (diagrams, code, labels, breadcrumbs): IBM Plex Mono

## The Rail (signature component)

`.rail` renders Foundgine's own execution pipeline as a connected chip sequence. Reuse it verbatim wherever the docs reference `Caller → Intent → ... → Result` or any sub-sequence of it, so readers recognize the same shape on every page. Add `.is-current` to the step most relevant to that page's content (e.g. the Performance page highlights "Provider").

Markup pattern:

```html
<div class="rail" role="img" aria-label="Pipeline: ...">
  <div class="rail-step"><span class="dot"></span>Step</div>
  <div class="rail-connector"></div>
  ...
</div>
```

Collapses to a vertical stack automatically under 620px — no extra markup needed.

## Other components

- `.converge` — funnel diagram for "many callers → Foundgine → many providers" (homepage, architecture).
- `.scenario` / `.scenario--allow` / `.scenario--deny` — color-coded authorization outcome cards (ai-agents page). Use `--allow` (signal blue) for permitted operations, `--deny` (gate amber) for denied/blocked ones — grounded in the actual allow/deny content, not decorative.
- `.toc` + `assets/toc.js` — sticky, scroll-spy table of contents for pages with 4+ `h2`/`h3` sections. Needs matching `id` attributes on headings and `href="#id"` links in the `<ol>`.
- Plain `<pre>` — for genuinely hierarchical/tree content (e.g. semantic vs. persistence model trees) where a linear rail doesn't fit.

## Adding a new page

1. Copy the `<head>` block from an existing inner page (fonts, stylesheet, per-page meta/OG tags).
2. Wrap content in `<div class="page has-toc"><main class="article">...</main><aside class="toc">...</aside></div>` if it has multiple sections; drop `has-toc` and the `<aside>` for short pages.
3. Add the page to `sitemap.xml`, `llms.txt`, and the header/footer nav on every other page.
