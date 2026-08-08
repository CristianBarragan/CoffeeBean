[Home](../../README.md) → [Documentation](../README.md) → [AI & LLM Readiness](README.md) → **LLM Readiness**

# LLM Readiness

## Contents

- [What llms.txt is](#what-llmstxt-is)
- [How Coffee Beanery uses it](#how-coffee-beanery-uses-it)
- [Keeping it accurate](#keeping-it-accurate)

---

## What llms.txt is

[`llms.txt`](https://llmstxt.org/) is a proposed convention — a plain-Markdown index at a
project's root that gives AI assistants and LLM-based tools a concise, curated map of a
project's documentation, instead of forcing them to crawl and guess at an entire site.
`llms-full.md` is the companion convention for a single, complete concatenation of the
underlying docs, for tools that ingest one file rather than following links.

## How Coffee Beanery uses it

- **`/llms.txt`** — a short, curated index pointing at each section of this documentation
  set, in the same order as [the docs hub](../README.md).
- **`/llms-full.md`** — the full content of this documentation set concatenated into one
  file, for tools that prefer a single ingest target.
- **`/AI.SEO.md`** — kept as an alias of `llms.txt`'s intent for tools that specifically look
  for an `AI.SEO.md` / `ai-seo.md` file at the repository root, so discovery doesn't depend
  on which convention a given tool implements.

All three are **generated from this documentation set**, not maintained by hand — see
[Keeping it accurate](#keeping-it-accurate).

## Keeping it accurate

Previously, `llms.txt`, `llms-full.md`, and `AI.SEO.md` had drifted into three
byte-for-byte identical copies of one draft document, unrelated to the actual `docs/`
structure. That's fixed as part of this restructuring — see the
[archive](../../docs/archive/README.md) for what the old copies looked like. Going forward,
regenerate all three whenever a section is added or renamed under `docs/`, so an LLM
reading `llms.txt` sees the same section list a human sees at [`docs/README.md`](../README.md).

---

## Related Documentation

- [Documentation Home](../README.md)
- [Reference](../13-Reference/README.md)

---

← Previous: [AI & LLM Readiness](README.md)  |  Next: [Performance](../10-Performance/README.md) →
