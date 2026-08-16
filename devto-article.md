---
title: We benchmarked an AI agent with vs. without a semantic execution boundary. It cut token load ~63% — and that's before you count the electricity.
published: false
tags: ai, dotnet, opensource, softwarearchitecture
canonical_url: https://cristianbarragan.github.io/Foundgine/agent-benchmark/
cover_image:
---

## The question

When you give an AI agent tools to complete a real business task, how much of what it does is *the task*, and how much is just the agent finding its footing — discovering the schema, pulling raw rows into context, re-reading them, hoping it didn't miss a field?

We built a paired benchmark to measure that gap directly, using [Foundgine](https://github.com/cristianbarragan/Foundgine), an open-source .NET semantic execution layer, against a conventional "give the agent raw application tools" flow. Same task, same data, same required final state. Only the execution boundary changes.

## The scenario

A banking customer-review task, deliberately not trivial:

```
Customer
  ├── 4 banking relationships
  │     └── 12 contracts
  │            └── 48 transactions
  ├── calculate total exposure
  ├── compare with a $48,000 threshold
  ├── mark the customer as reviewed (one field mutation)
  └── verify the final state
```

**Flow A — Conventional application/AI:** the agent gets application tools for schema discovery, customer lookup, relationship lookup, contract lookup, transaction retrieval, mutation, and verification. It has to reconstruct the graph itself, in its own context, one tool call at a time.

**Flow B — Foundgine semantic flow:** the agent gets a semantic capability. Foundgine resolves the graph, applies the application's authorization boundary, and executes behind that boundary. The agent asks for the outcome; it doesn't walk the schema to get there.

Both flows are graded on one thing above everything else: did they land on the *exact same final state*? If not, nothing else on this page matters. Across 10 measured runs (3 warmups) for each flow, they did — 100% of the time.

## What actually changed

| Metric | Conventional | Foundgine | Change |
|---|---|---|---|
| Tool calls | 7 | 4 | **−42.9%** |
| Est. token load / call (heuristic) | ~981 | ~364 | **−62.9%** |
| Same final state | ✅ | ✅ | pass |

The tool-call number is measured directly from the harness — no estimation involved. The token number needs one more step, because this benchmark runs in `replay` mode (no live model calls, so provider-reported token counts are correctly zero — that's the harness working as intended, not a gap). To fill that in, we applied the standard tokenizer approximation — `tokens ≈ max(chars/4, words×1.3)` — to every recorded tool input/output payload plus the fixed system prompt, which tracks real BPE tokenizers within roughly ±15% for payloads like these. It's directional, not a provider-reported measurement, and it doesn't include the model's own reasoning tokens (which a live run would add — to both flows).

## What that's worth in dollars

Converting token load into cost needs one more inference: tool-output payloads get billed as **input** tokens on the agent's next turn, tool-input payloads (the args the model generated) get billed as **output** tokens. That's how a real tool-calling loop is billed — inferred here, not measured, but it's the standard convention.

At current Claude API list pricing (Aug 2026):

| Model ($/MTok in/out) | Saved / call | 100K calls/day |
|---|---|---|
| Haiku 4.5 ($1/$5) | $0.000685 | ~$2,055/mo · ~$25K/yr |
| Sonnet 5, standard ($3/$15) | $0.002055 | ~$6,165/mo · ~$75K/yr |
| Opus 5 ($5/$25) | $0.003425 | ~$10,275/mo · ~$125K/yr |

A single internal agent running 10K calls/day is a few hundred dollars a month. A platform-wide agent at 1M calls/day is six figures a month. **Treat every one of these as an order of magnitude for planning, not a quote** — it's a heuristic estimate at list price, reproducible with a small script (`estimate_cost_savings.py`, shipped in the repo) so you can re-run it with your own volume and pricing instead of trusting mine.

## The part that surprised us: Foundgine was *slower*, wall-clock

This is the section a lot of benchmark posts would quietly drop. We didn't.

In this replay, Foundgine's wall-clock time was *higher*, not lower — the conventional flow's 7 small round trips were individually cheap in application time; Foundgine's 4 calls do more resolution work behind the boundary before the agent ever sees a result. Fewer, smaller round trips for the agent traded against more work inside the application.

Tokens, API time, and application load are three different measurements, and a flow can win on one while losing another. Optimizing for per-call API spend and context-window pressure favors the semantic boundary here. Optimizing for raw end-to-end latency does not automatically follow. We report both instead of netting them out into one number that flatters the result.

## Zooming out: what if this were the world's problem, not one benchmark?

This part is explicitly a napkin calculation, not a claim. But it's worth doing once, in the open, so it can be argued with.

The IEA's 2026 base case puts global data-center electricity at roughly 485 TWh (2025) heading toward ~950 TWh by 2030, with AI-optimized servers already at ~31% of 2026 data-center power draw (~175 TWh/year) and growing about 3x faster than conventional server load. Critically, the IEA specifically flags **agentic and reasoning workloads as consuming hundreds to thousands of times more energy per query than a simple text prompt** — this is exactly the workload class this benchmark is measuring.

If we assume energy scales roughly with tokens processed (a simplification — attention cost is superlinear in context length, so this probably understates the real gap at longer contexts), and some slice of that ~175 TWh/year AI-server budget is agentic tool-calling traffic shaped like our "conventional" flow, applying the measured ~63% token-load cut:

| Share of AI-server power that's agentic tool-calling like this | ≈ energy | ≈ saved at 63% cut | @ ~$0.12/kWh |
|---|---|---|---|
| 1% | 1.75 TWh/yr | 1.1 TWh/yr | ~$132M/yr |
| 5% | 8.75 TWh/yr | 5.5 TWh/yr | ~$660M/yr |
| 10% | 17.5 TWh/yr | 11 TWh/yr | ~$1.3B/yr |

1.1 TWh/year is roughly the annual electricity draw of 100,000 average US homes. None of the percentages in the left column are measured — they're scenario inputs, and I'd genuinely like people to push back on them. The one number underneath all three rows that *is* measured is the 62.9% token-load reduction on this specific scenario. The rest is "if this generalizes, here's the shape of what it's worth" — in dollars, and, just as importantly, in kilowatt-hours and tonnes of CO₂e.

The honest takeaway isn't the headline dollar or TWh figure. It's that a meaningful share of agentic AI's cost and energy footprint is going toward **an agent re-discovering how to talk to an application**, not toward the business logic itself. That's a fixable, boring, structural problem — not an inherent cost of using AI agents.

## Efficiency is not the same thing as safety — and shouldn't be sold as one

This is the part I want to be most direct about, because "we made the agent cheaper" is a dangerous headline to leave unqualified.

Cutting tool calls and token load does **not**, by itself, change what an agent is authorized to do. That has to be designed in, deliberately, independent of the efficiency win:

- **The application stays the authority, not the model.** Foundgine's semantic capability and mutation boundary are application-defined; the agent requests intent, it never gets raw SQL or physical schema access. A cheaper path can't quietly become a wider one.
- **Mutations require explicit, narrow intent.** In this benchmark, the only field either flow is allowed to mutate is `Customer.FullName` — because that's the one field the capability grants, not because the agent chose to be conservative.
- **Verification happens regardless of token count.** Both flows call a verify step after the mutation. Fewer tokens should never mean fewer checks.
- **"Same final state" is the actual gate for every number on this page.** An agent that's 63% cheaper and reaches the *wrong* state isn't a result — it's a regression that happens to look fast.

The efficiency case and the safety case for a semantic execution boundary aren't competing goals. They're the same design decision, looked at from two angles. If you're building agent infrastructure and only optimizing for tokens, you're only doing half the job.

## Reproduce it yourself

- Full technical report, run-by-run trace, and every caveat: [agent benchmark → technical report](https://cristianbarragan.github.io/Foundgine/agent-benchmark/detail.html)
- Source: [github.com/cristianbarragan/Foundgine](https://github.com/cristianbarragan/Foundgine)
- `scripts/estimate_cost_savings.py` — pass your own `--calls-per-day` and `--input-price`/`--output-price` and get your own number instead of mine.

Push back on the assumptions. That's the point of publishing the method next to the number.
