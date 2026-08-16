(() => {
  const table = document.querySelector('[data-report-table]');
  const status = document.querySelector('[data-report-status]');
  const tokenBox = document.querySelector('[data-token-estimate]');
  const source = document.querySelector('[data-report-source]')?.dataset.source || '../assets/agent-benchmark.json';
  if (!table && !status && !tokenBox) return;

  const esc = v => String(v ?? '').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;').replaceAll("'",'&#039;');
  const fmt = v => typeof v === 'number' ? v.toLocaleString(undefined,{maximumFractionDigits:1}) : '—';
  const change = (a,b) => !a ? '—' : `${((b-a)/a*100>0?'+':'')}${((b-a)/a*100).toFixed(1)}%`;
  const saved = (a,b) => !a ? '—' : `${(((a-b)/a)*100).toFixed(1)}%`;

  // --- Offline, heuristic token-load estimate -----------------------------------------------
  // The harness's provider-reported InputTokens/OutputTokens/TotalTokens are correctly 0 in
  // `replay` mode (see Program.cs: TraceCollector never sees a real ModelCall). That's accurate
  // behaviour, not a bug — but it leaves the token dimension blank on this page. This estimate
  // fills that gap from the recorded tool Input/Output payloads using the standard chars/words
  // heuristic (tokens ~= max(chars/4, words*1.3)), which tracks real BPE tokenizers within
  // roughly +/-15% for payloads like these. It is NOT a substitute for provider-reported usage
  // from a `live` run, and it does NOT include the model's own reasoning/response tokens.
  const estimateTokens = t => {
    if (!t) return 0;
    const chars = String(t).length;
    const words = String(t).trim().split(/\s+/).filter(Boolean).length;
    return Math.round(Math.max(chars / 4, words * 1.3));
  };

  const SYSTEM_PROMPTS = {
    'Conventional application/AI flow': 'You are a careful banking application agent. Use the available application tools to complete the request. You must inspect the schema before querying unfamiliar data, never invent fields, and verify the final state after a mutation.',
    'Foundgine semantic flow': 'You are a careful banking agent using Foundgine. Treat the semantic capability and graph/mutation tools as the authoritative domain interface. Do not request raw SQL or physical schema details. Verify the final state after a mutation.',
  };
  const SCENARIO_REQUEST = "Review Customer 1 in the benchmark fixture. Traverse the customer's banking relationships, contracts and transactions and calculate total exposure as the sum of transaction Balance values. If exposure is at least 48,000, mark the customer as reviewed by setting FullName to exactly `Customer 1 Benchmark | Reviewed`. Then verify the final state. Return customer key, relationship count, contract count, transaction count, exposure and final full name. Do not modify any other customer or business data.";

  function estimateRun(run) {
    const trace = run.Trace || [];
    let toolIn = 0, toolOut = 0;
    trace.forEach(ev => { toolIn += estimateTokens(ev.Input); toolOut += estimateTokens(ev.Output); });
    return { toolIn, toolOut, total: toolIn + toolOut };
  }

  // Bug fix: key the system-prompt lookup off each run's own `Flow` field (e.g.
  // "Conventional application/AI flow"), NOT the outer `flow.title` from the report's `Flows`
  // array (which is worded/spaced differently, e.g. "Conventional application / AI"). Keying off
  // `flow.title` silently misses the SYSTEM_PROMPTS lookup and zeroes out the fixed system-prompt
  // overhead for that flow, understating its estimated context load.
  function estimateFlowAgg(flow) {
    const runs = (flow.runs || []).filter(r => Array.isArray(r.Trace));
    const flowKey = (runs[0] && runs[0].Flow) || flow.title;
    const overhead = estimateTokens(SYSTEM_PROMPTS[flowKey] || '') + estimateTokens(SCENARIO_REQUEST);
    const perRun = runs.map(estimateRun);
    const n = perRun.length || 1;
    const avgIn = perRun.reduce((a, r) => a + r.toolIn, 0) / n;
    const avgOut = perRun.reduce((a, r) => a + r.toolOut, 0) / n;
    return { overhead, avgIn, avgOut, avgContextLoad: overhead + avgIn + avgOut, flowKey };
  }

  // Adapter: the .NET harness writes a flat `Results` array (each run tagged with a
  // `Flow` string), not the nested `Flows:[{title,runs}]` shape this page's rendering
  // code expects. Build the nested shape here so both report layouts work without
  // needing two copies of the estimator logic (mirrors scripts/estimate_cost_savings.py).
  function toFlows(report) {
    if (Array.isArray(report.Flows) && report.Flows.length >= 2) return report.Flows;
    if (!Array.isArray(report.Results)) return [];
    const order = ['Conventional application/AI flow', 'Foundgine semantic flow'];
    const byFlow = new Map();
    report.Results.forEach(r => {
      if (!byFlow.has(r.Flow)) byFlow.set(r.Flow, []);
      byFlow.get(r.Flow).push(r);
    });
    return order.filter(title => byFlow.has(title)).map(title => ({ title, runs: byFlow.get(title) }));
  }

  // Reference USD/million-token list prices, current as of Aug 2026 — mirrors
  // REFERENCE_MODELS in scripts/estimate_cost_savings.py. Pass real prices for your
  // own model if these drift.
  const REFERENCE_MODELS = [
    ['Haiku 4.5', 1, 5],
    ['Sonnet 5 (intro, through Aug 31 2026)', 2, 10],
    ['Sonnet 5 (standard, from Sep 1 2026)', 3, 15],
    ['Opus 5', 5, 25],
  ];

  function costTable(estConv, estFound) {
    // Billing convention: tool-output payloads are fed back as INPUT tokens on the
    // agent's next turn; tool-input payloads (args the model generated) are billed as
    // OUTPUT tokens. Inferred from how a tool-calling loop is normally billed, not
    // measured — see scripts/estimate_cost_savings.py for the same math offline.
    const convIn = estConv.overhead + estConv.avgOut, convOut = estConv.avgIn;
    const foundIn = estFound.overhead + estFound.avgOut, foundOut = estFound.avgIn;
    const vols = [10000, 100000, 1000000];
    let rows = '';
    REFERENCE_MODELS.forEach(([name, pin, pout]) => {
      const cc = convIn / 1e6 * pin + convOut / 1e6 * pout;
      const fc = foundIn / 1e6 * pin + foundOut / 1e6 * pout;
      const savedPerCall = cc - fc;
      const cells = vols.map(v => `$${Math.round(savedPerCall * v * 30).toLocaleString()}/mo`);
      const yearAt100k = Math.round(savedPerCall * 100000 * 365).toLocaleString();
      rows += `<tr><td>${esc(name)} ($${pin}/$${pout})</td><td>$${savedPerCall.toFixed(6)}</td><td>${cells[0]}</td><td>${cells[1]}</td><td>${cells[2]}</td><td>$${yearAt100k}/yr</td></tr>`;
    });
    return `<table class="report-table"><thead><tr><th>Model (list price / MTok)</th><th>Saved / call</th><th>10K calls/day</th><th>100K calls/day</th><th>1M calls/day</th><th>100K calls/day, annualized</th></tr></thead><tbody>${rows}</tbody></table>`;
  }

  fetch(source, { headers: { Accept: 'application/json' } })
    .then(r => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
    .then(report => {
      const c = report.Comparison.Conventional, f = report.Comparison.Foundgine, cmp = report.Comparison;
      const hasProviderTokens = !!(cmp.HasProviderTokenData ?? (c.TotalTokens > 0 || f.TotalTokens > 0));

      if (status) status.innerHTML = `<span class="report-badge">published result</span> ${esc(report.Mode)} · ${esc(report.GeneratedAtUtc)}`;

      if (table) {
        const tokenRow = hasProviderTokens
          ? `<tr><td>Total tokens (provider-reported)</td><td>${fmt(c.TotalTokens)}</td><td>${fmt(f.TotalTokens)}</td><td>${change(c.TotalTokens, f.TotalTokens)}</td></tr>`
          : `<tr><td>Total tokens (provider-reported)</td><td colspan="3">N/A — ${esc(report.Mode)} mode makes no model calls. See the estimate below.</td></tr>`;
        table.innerHTML = `<table class="report-table"><thead><tr><th>Metric</th><th>Conventional</th><th>Foundgine</th><th>Change</th></tr></thead><tbody>
          <tr><td>Wall clock (ms)</td><td>${fmt(c.WallClockMs)}</td><td>${fmt(f.WallClockMs)}</td><td>${change(c.WallClockMs,f.WallClockMs)}</td></tr>
          <tr><td>Tool time (ms)</td><td>${fmt(c.ToolTimeMs)}</td><td>${fmt(f.ToolTimeMs)}</td><td>${change(c.ToolTimeMs,f.ToolTimeMs)}</td></tr>
          <tr><td>Model calls</td><td>${fmt(c.ModelCalls)}</td><td>${fmt(f.ModelCalls)}</td><td>${change(c.ModelCalls,f.ModelCalls)}</td></tr>
          <tr><td>Tool calls</td><td>${fmt(c.ToolCalls)}</td><td>${fmt(f.ToolCalls)}</td><td>${change(c.ToolCalls,f.ToolCalls)}</td></tr>
          ${tokenRow}
          <tr><th>Same final state</th><td colspan="2">${cmp.SameFinalState?'TRUE':'FALSE'}</td><td>${cmp.SameFinalState?'PASS':'FAIL'}</td></tr>
        </tbody></table>`;
      }

      if (tokenBox) {
        const flows = toFlows(report);
        if (flows.length < 2) {
          tokenBox.textContent = 'No per-run trace data in this report — run the benchmark with recorded runs to see the estimate.';
          return;
        }
        const estConv = estimateFlowAgg(flows[0]);
        const estFound = estimateFlowAgg(flows[1]);
        tokenBox.innerHTML = `<table class="report-table"><thead><tr><th>Metric</th><th>${esc(estConv.flowKey)}</th><th>${esc(estFound.flowKey)}</th><th>Change</th></tr></thead><tbody>
          <tr><td>System prompt + request (fixed, per run)</td><td>~${estConv.overhead}</td><td>~${estFound.overhead}</td><td>—</td></tr>
          <tr><td>Avg. tool-input tokens / run</td><td>~${fmt(estConv.avgIn)}</td><td>~${fmt(estFound.avgIn)}</td><td>${change(estConv.avgIn, estFound.avgIn)}</td></tr>
          <tr><td>Avg. tool-output tokens / run</td><td>~${fmt(estConv.avgOut)}</td><td>~${fmt(estFound.avgOut)}</td><td>${change(estConv.avgOut, estFound.avgOut)}</td></tr>
          <tr><th>Avg. estimated context load / run</th><th>~${fmt(estConv.avgContextLoad)}</th><th>~${fmt(estFound.avgContextLoad)}</th><th>${saved(estConv.avgContextLoad, estFound.avgContextLoad)} lower</th></tr>
        </tbody></table>`;

        const costBox = document.querySelector('[data-cost-estimate]');
        if (costBox) costBox.innerHTML = costTable(estConv, estFound);
      }
    })
    .catch(e => {
      if (status) status.textContent = `Benchmark report could not load: ${e.message}`;
      if (tokenBox) tokenBox.textContent = `Token estimate could not load: ${e.message}`;
    });
})();
