/* Foundgine unified benchmark reporting. Generated report data is measurement-first:
   provider token usage is shown when present; replay/token estimates are explicitly labelled. */
(() => {
 const root=document.querySelector('[data-benchmark-run]');
 if(!root)return;
 const run=root.dataset.benchmarkRun;
 const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const fmt=(n,d=1)=>Number(n??0).toLocaleString(undefined,{maximumFractionDigits:d});
 const pct=(a,b)=>b?((a/b)-1)*100:0;
 const el=document.createElement('section'); el.className='benchmark-evidence-frame'; el.id='measurement-report';
 el.innerHTML='<div class="evidence-frame-head"><div><div class="run-label">Unified measurement report</div><h2>All published measurements, in one place</h2><p>Every run uses the same reporting vocabulary: throughput, latency distribution, success/failure, agent/tool work, estimated context, and clearly labelled cost/energy estimates. Values derived from replay payloads are estimates, not provider billing or measured power draw.</p></div></div><div id="br-summary" class="run-intro-grid"></div><div id="br-table" class="table-wrap" style="margin-top:1rem"></div><p class="matrix-note" style="margin-top:1rem"><strong>Important:</strong> estimated context tokens use the benchmark heuristic. Annual cost assumes 100,000 flows/day at $3 per million estimated context tokens. Annual energy is an illustrative conversion at 0.30 Wh/1,000 estimated tokens. Neither is a measured utility bill, carbon footprint, or provider invoice.</p>';
 root.parentNode.insertBefore(el,root.nextSibling);
 fetch(`../../assets/agent-benchmark/run${run}-aggregate.json`,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(r.status);return r.json()}).then(d=>{
   const rows=d.aggregate||[];
   const impl=[...new Set(rows.map(x=>x.implementation))];
   const variants=[...new Set(rows.map(x=>x.option||'standard'))];
   const customers=[...new Set(rows.map(x=>x.customers))].sort((a,b)=>a-b);
   const conc=[...new Set(rows.map(x=>x.concurrency).filter(x=>x))].sort((a,b)=>a-b);
   const success=rows.reduce((s,x)=>s+Number(x.success||0),0), failed=rows.reduce((s,x)=>s+Number(x.failed||0),0);
   const avgCtx=rows.length?rows.reduce((s,x)=>s+Number(x.estimatedContextTokens||0),0)/rows.length:0;
   const avgRps=rows.length?rows.reduce((s,x)=>s+Number(x.rps||0),0)/rows.length:0;
   const avgWall=rows.length?rows.reduce((s,x)=>s+Number(x.avgWallMs||0),0)/rows.length:0;
   const avgTools=rows.length?rows.reduce((s,x)=>s+Number(x.toolCalls||0),0)/rows.length:0;
   document.getElementById('br-summary').innerHTML=[
    ['Cells / measurements',`${rows.length}`],
    ['Implementations',impl.join(' · ')],
    ['Workloads',customers.map(x=>x.toLocaleString()).join(' · ')],
    ['Concurrency',conc.length?conc.map(x=>'C'+x).join(' · '):'single scenario'],
    ['Avg RPS',fmt(avgRps,1)],
    ['Avg wall',fmt(avgWall,2)+' ms'],
    ['Avg estimated context',fmt(avgCtx,0)+' tokens'],
    ['Avg tool calls',fmt(avgTools,2)],
    ['Success / failed',`${success.toLocaleString()} / ${failed.toLocaleString()}`]
   ].map(([a,b])=>`<div class="run-intro-card"><strong>${esc(a)}</strong><span>${esc(b)}</span></div>`).join('');
   const cols=['customers','concurrency','option','implementation','samples','rps','avgWallMs','p50Ms','p95Ms','p99Ms','maxWallMs','success','failed','toolCalls','estimatedInputTokens','estimatedOutputTokens','estimatedContextTokens','annualCostUsd','annualEnergyKwh','efficiencyRpsPer1kTokens'];
   const labels={customers:'Customers',concurrency:'Concurrency',option:'Variant',implementation:'Implementation',samples:'Samples',rps:'RPS',avgWallMs:'Avg wall (ms)',p50Ms:'P50 (ms)',p95Ms:'P95 (ms)',p99Ms:'P99 (ms)',maxWallMs:'Max (ms)',success:'Success',failed:'Failed',toolCalls:'Tool calls',estimatedInputTokens:'Est. input tokens',estimatedOutputTokens:'Est. output tokens',estimatedContextTokens:'Est. context tokens',annualCostUsd:'Est. annual cost (USD)',annualEnergyKwh:'Est. annual energy (kWh)',efficiencyRpsPer1kTokens:'RPS / 1k est. tokens'};
   const money=x=>x==null?'—':'$'+fmt(x,2), num=(k,x)=>x==null?'—':k==='annualCostUsd'?money(x):k==='annualEnergyKwh'?fmt(x,2):fmt(x,k==='rps'||k==='avgWallMs'||k==='p50Ms'||k==='p95Ms'||k==='p99Ms'||k==='maxWallMs'?2:1);
   document.getElementById('br-table').innerHTML=`<div style="overflow:auto"><table class="impact-table"><thead><tr>${cols.map(c=>`<th>${labels[c]}</th>`).join('')}</tr></thead><tbody>${rows.map(x=>`<tr>${cols.map(c=>`<td>${esc(c==='option'?(x[c]||'standard'):c==='implementation'?x[c]:num(c,x[c]))}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
 }).catch(e=>{el.querySelector('#br-table').innerHTML='<p>Published aggregate data is not available yet. Run the benchmark publisher to generate the unified report.</p>';});
})();
