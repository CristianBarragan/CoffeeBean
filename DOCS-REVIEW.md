# Documentation Review — Foundgine

## Scope

Reviewed the uploaded repository as a documentation/source-of-truth exercise, including:

- repository layout
- solution/project references
- current `src/` projects
- source-generator structure
- Graphgine SQL/graph implementation
- Banking sample
- tests
- legacy tree
- existing `README.md`
- existing `llms.txt`
- existing `llms-full.md`
- existing root `ai.seo.md`
- detailed `docs/` tree
- Git remote/history metadata

## Main conclusion

The repository has a much clearer identity now than the old Coffee Beanery tree suggests:

**Foundgine = platform. Graphgine = first product. Coffee Beanery = historical predecessor.**

The biggest documentation problem is not lack of content. It is **stale identity and overclaiming**.

## High-priority problems found

### 1. Branding drift

The root `llms.txt`, `ai.seo.md`, and `llms-full.md` still describe Coffee Beanery as the current product.

That is now misleading.

**Fix:** use Foundgine as the platform identity and Graphgine as the product identity. Mention Coffee Beanery only as history/legacy.

### 2. `llms-full.md` is stale

The current file is approximately 200 KB and is a concatenation of documentation that still contains old names and old paths.

It should not be the raw concatenation of every page.

**Recommended model:**

```text
llms-full.md
    ↓ canonical AI context draft
    ├── llms.txt       concise retrieval context
    └── ai.seo.md      AI/search entity positioning
```

The supplied replacement follows this model.

### 3. Detailed docs contain stale paths

The repository has no `docs/README.md`, yet many pages link to it and `mkdocs.yml` uses it as the documentation home.

There are also references to the old `example/HotChocolateCoffeeBeanery` location even though the current sample is under:

`samples/Graphgine.Samples.Banking`

This should be fixed in the next documentation pass.

### 4. AI SEO path/link errors

`docs/AI.SEO.md` contains links that are effectively rooted incorrectly for a file already inside `docs/`.

The root `ai.seo.md` also contains generated relative links that point to headings such as `Schema.md` without preserving their original directory.

This makes the AI SEO material less useful as a navigation source.

### 5. Production-readiness overstatement

The source tree contains real incomplete implementation.

Confirmed incomplete areas include:

- Foundgine SQL execution provider
- Foundgine graph execution provider
- Foundgine cache execution provider
- Graphgine graph strategy
- Graphgine TODOs in selection/mutation planning
- placeholder projects
- placeholder tests
- incomplete sample wiring

Documentation should explicitly separate:

**architectural intent**

from

**currently implemented behavior**.

### 6. Sample is not currently a safe quick-start claim

The Banking sample contains references to `IProcessService` and `AddGraphgine`, while the current project graph does not include the historical `CoffeeBeanery` service project.

Therefore the sample should not be advertised as “clone and run” until its wiring is repaired and a CI build proves it.

### 7. Test coverage is scaffolding

Both current test projects contain placeholder tests.

The documentation should not imply meaningful automated coverage yet.

## Recommended documentation hierarchy

### Root README

Human-first.

Answer:

1. What is Foundgine?
2. What is Graphgine?
3. Why the separation?
4. What exists now?
5. What is incomplete?
6. How is the repository organised?
7. Where should a developer go next?

### `llms-full.md`

AI-first canonical factual context.

It should include:

- identity
- terminology
- architecture
- project responsibilities
- runtime flow
- source generation
- persistence
- sample status
- known gaps
- accuracy rules
- roadmap

It should avoid copying every prose page from `docs/`.

### `llms.txt`

Short retrieval document.

It should answer:

- what it is
- where important code lives
- what is implemented
- what is incomplete
- terminology rules
- high-value source locations

### `ai.seo.md`

Search/AI discovery document.

It should contain:

- canonical names
- search phrases
- product positioning
- concise definitions
- comparisons to adjacent technologies
- current status
- AI answer templates

## Architecture wording to standardise

Use:

> Foundgine is the platform; Graphgine is the first product.

Use:

> Graphgine integrates with Hot Chocolate.

Do not use:

> Graphgine replaces Hot Chocolate.

Use:

> Graphgine can consume EF Core mapping information.

Do not use:

> Graphgine is an ORM.

Use:

> The architecture is provider-oriented and currently PostgreSQL-focused.

Do not use:

> Foundgine already supports arbitrary database providers.

## Suggested next documentation pass

After these four root files are adopted, normalise `docs/` in this order:

1. create/fix `docs/README.md`
2. remove Coffee Beanery branding from current docs
3. fix all `example/` paths
4. fix relative AI SEO links
5. align the architecture pages with the current project graph
6. mark incomplete features explicitly
7. add a single “Current Status” page
8. add architecture dependency tests
9. make CI validate documentation links
10. regenerate any machine-ingest files only after the human docs are canonical

## Deliverables in this draft pack

- `README.md`
- `llms.txt`
- `llms-full.md`
- `ai.seo.md`
- `DOCS-REVIEW.md`

These are intentionally conservative about current capabilities. They are designed to be the factual baseline before the detailed documentation is cleaned up.
