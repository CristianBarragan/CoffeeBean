#!/usr/bin/env python3
"""Build the canonical published benchmark report data from docs-site assets.
No provider billing or power measurements are invented; derived cost/energy values
are explicitly estimates from the documented token heuristic."""
import json, math, os, statistics, re
from datetime import datetime, timezone

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__), "..","..",".."))
ASSET=os.path.join(ROOT,"docs-site","assets","agent-benchmark")
ASS={"callsPerDay":100000,"inputUsdPerMillionTokens":3.0,"energyWhPer1000Tokens":0.30,
     "energyNote":"Illustrative estimate from heuristic context tokens; not measured power draw.",
     "costNote":"Illustrative annual estimate at 100,000 flows/day and $3/M estimated context tokens."}
def load(p):
    with open(p,encoding="utf-8-sig") as f:return json.load(f)
def dump(p,x):
    os.makedirs(os.path.dirname(p),exist_ok=True)
    with open(p,"w",encoding="utf-8") as f:json.dump(x,f,indent=2,ensure_ascii=False)
def enrich(x):
    ctx=float(x.get("estimatedContextTokens",0) or 0); rps=float(x.get("rps",0) or 0)
    x["annualCostUsd"]=ctx/1e6*ASS["callsPerDay"]*365*ASS["inputUsdPerMillionTokens"]
    x["annualEnergyKwh"]=ctx/1000*ASS["callsPerDay"]*365*ASS["energyWhPer1000Tokens"]/1000
    x["efficiencyRpsPer1kTokens"]=rps/(ctx/1000) if ctx else 0
    x.setdefault("providerInputTokens",0)
    x.setdefault("providerOutputTokens",0)
    x.setdefault("providerTotalTokens",0)
    x["tokenAccounting"]="provider-reported" if x["providerTotalTokens"] else "estimated-context"
    return x
def pct(vals,p):
    a=sorted(vals)
    if not a:return 0
    pos=(len(a)-1)*p; lo=math.floor(pos); hi=math.ceil(pos)
    return a[lo] if lo==hi else a[lo]+(a[hi]-a[lo])*(pos-lo)
def agent_family(run):
    out=[]
    base=os.path.join(ASSET,run)
    for dp,_,fs in os.walk(base):
        if "agent-benchmark.json" not in fs:continue
        d=load(os.path.join(dp,"agent-benchmark.json")); cfg=d.get("Configuration") or {}
        # Run 2 predates the shared Configuration envelope and stores these at
        # the report root. Run 1/3 use Configuration.CustomerCount/Concurrency.
        cust=cfg.get("CustomerCount",d.get("CustomerCount"))
        conc=cfg.get("Concurrency",d.get("Concurrency"))
        if cust is None:
            # A Run 2 artifact is scoped by its published directory when the
            # report itself does not carry CustomerCount.
            m=re.search(r"(?:^|[\\/])0*(\d+)-customers(?:$|[\\/])",dp)
            cust=int(m.group(1)) if m else None
        if conc is None:
            m=re.search(r"(?:^|[\\/])concurrency-(\d+)(?:$|[\\/])",dp)
            conc=int(m.group(1)) if m else None
        if cust is None or conc is None:
            raise ValueError(f"Could not determine customer count/concurrency for {os.path.join(dp,'agent-benchmark.json')}")
        for flow,impl in [("Conventional application/AI flow","Conventional"),("Conventional","Conventional"),
                          ("Foundgine semantic flow","Foundgine"),("Foundgine","Foundgine")]:
            vals=[x for x in d.get("Results",[]) if x.get("Flow")==flow]
            if not vals:continue
            def av(k):return sum(float(x.get(k,0) or 0) for x in vals)/len(vals)
            walls=[float(x.get("WallClockMs",0) or 0) for x in vals]; wall=av("WallClockMs")
            ctx=av("EstimatedContextLoadTokens")
            # Run 2 may expose provider fields rather than the Run1 names.
            tin=av("EstimatedToolInputTokens")
            tout=av("EstimatedToolOutputTokens")
            provider_in=av("ProviderInputTokens")
            provider_out=av("ProviderOutputTokens")
            provider_total=av("ProviderTotalTokens")
            x={"customers":int(cust),"concurrency":int(conc),"implementation":impl,"option":"standard","samples":len(vals),
               "rps":conc*1000/wall if wall else 0,"avgWallMs":wall,"p50Ms":pct(walls,.5),"p95Ms":pct(walls,.95),"p99Ms":pct(walls,.99),
               "maxWallMs":max(walls),"success":sum(1 for v in vals if v.get("FinalState")),"failed":sum(1 for v in vals if not v.get("FinalState")),"toolCalls":av("ToolCalls"),"logicalOps":1,
               "estimatedInputTokens":tin,"estimatedOutputTokens":tout,"estimatedContextTokens":av("EstimatedContextLoadTokens"),
               "providerInputTokens":provider_in,"providerOutputTokens":provider_out,"providerTotalTokens":provider_total}
            out.append(enrich(x))
    return out
for run in ["run1","run2","run3"]:
    rows=agent_family(run)
    dump(os.path.join(ASSET,f"{run}-aggregate.json"),{"schemaVersion":3,"run":run,"reportType":"measured-comparison","assumptions":ASS,"aggregate":rows})
# Run 4: preserve both agent and protocol variants, aggregated by cell.
r4=[]
for dp,_,fs in os.walk(os.path.join(ASSET,"run4")):
    if "run4-metadata.json" not in fs:continue
    d=load(os.path.join(dp,"run4-metadata.json"))
    for s in d.get("samples",[]):
        r4.append({"customers":int(s["customerCount"]),"concurrency":int(s["concurrency"]),
          "implementation":"Conventional" if s["implementation"]=="Conventional" else "Foundgine",
          "option":"agent" if "agent" in s["option"].lower() else "protocol",
          "rps":float(s.get("rps",0)),"avgWallMs":float(s.get("avgWallMs",0)),"p50Ms":float(s.get("p50Ms",0)),
          "p95Ms":float(s.get("p95Ms",0)),"p99Ms":float(s.get("p99Ms",0)),"maxWallMs":float(s.get("maxWallMs",0)),
          "success":int(s.get("success",0)),"failed":int(s.get("failed",0)),"toolCalls":float(s.get("toolCalls",0)),
          "estimatedInputTokens":float(s.get("estimatedInputTokens",0)),"estimatedOutputTokens":float(s.get("estimatedOutputTokens",0)),
          "estimatedContextTokens":float(s.get("estimatedContextTokens",0)),"logicalOps":1})
g={}
for x in r4:g.setdefault((x["customers"],x["concurrency"],x["implementation"],x["option"]),[]).append(x)
agg=[]
for k,v in g.items():
    x={"customers":k[0],"concurrency":k[1],"implementation":k[2],"option":k[3],"samples":len(v)}
    for f in ["rps","avgWallMs","p50Ms","p95Ms","p99Ms","toolCalls","estimatedInputTokens","estimatedOutputTokens","estimatedContextTokens","logicalOps"]:
        x[f]=sum(z[f] for z in v)/len(v)
    x["maxWallMs"]=max(z["maxWallMs"] for z in v); x["success"]=sum(z["success"] for z in v); x["failed"]=sum(z["failed"] for z in v); enrich(x); agg.append(x)
dump(os.path.join(ASSET,"run4-aggregate.json"),{"schemaVersion":3,"run":"run4","reportType":"measured-comparison","variants":["agent","protocol"],"assumptions":ASS,"aggregate":agg})
# Run 5 / 5b use their published metadata.
def metadata_family(run,kind):
    out=[]
    asset_run = 'run5-same-client' if run == 'run5b' else run
    for dp,_,fs in os.walk(os.path.join(ASSET,asset_run)):
        fn=f"{kind}-metadata.json"
        if fn not in fs:continue
        d=load(os.path.join(dp,fn))
        groups={}
        if kind=="run5":
            batch_size=int(d.get("batchSize",1) or 1)
            for s in d.get("samples",[]):
                raw_impl=s["implementation"]
                impl="MCP + Foundgine" if "Foundgine" in raw_impl else "MCP + EF Core"
                divisor=batch_size if impl=="MCP + Foundgine" else 1
                x={"customers":int(d["customers"]),"concurrency":int(d["concurrency"]),"implementation":impl,
                   "option":"standard","rps":float(s["Rps"]),"avgWallMs":float(s["AvgWallMs"]),"p50Ms":float(s["P50Ms"]),
                   "p95Ms":float(s["P95Ms"]),"p99Ms":float(s["P99Ms"]),"maxWallMs":float(s["MaxWallMs"]),
                   "success":int(s["Success"]),"failed":int(s["Failed"]),"toolCalls":float(s["ToolCalls"]),
                   "estimatedInputTokens":float(s["EstimatedInputTokens"])/divisor,"estimatedOutputTokens":float(s["EstimatedOutputTokens"])/divisor,
                   "estimatedContextTokens":(float(s["EstimatedInputTokens"])+float(s["EstimatedOutputTokens"])) / divisor,"logicalOps":batch_size if impl=="MCP + Foundgine" else 1}
                groups.setdefault((x["implementation"],),[]).append(x)
        else:
            concurrency=int(d["concurrency"])
            samples=d.get("samples",[])
            # The raw Run5b metadata stores per-task samples, while the runner's
            # published RPS is per concurrency batch: total logical operations
            # completed by the batch divided by its slowest task wall time.
            # Reconstruct that exact definition from the flattened samples.
            for impl_idx, impl_name in enumerate(["MCP + EF Core (same client, individual calls)", "MCP + Foundgine (same client, semantic batch)"]):
                impl_samples=[s for s in samples if s.get("implementation")==impl_name]
                for i in range(0, len(impl_samples), concurrency):
                    batch=impl_samples[i:i+concurrency]
                    if not batch: continue
                    ok=[s for s in batch if s["Success"]]
                    wall=max((float(s["WallMs"]) for s in ok), default=0)
                    batch_rps=(sum(float(s["LogicalOps"]) for s in ok)*1000/wall) if wall else 0
                    for s in batch:
                        ib=float(s["InputBytes"]); ob=float(s["OutputBytes"]); pb=float(s["TotalPayloadBytes"]); swall=float(s["WallMs"])
                        x={"customers":int(d["customers"]),"concurrency":concurrency,"implementation":s.get("Implementation",s.get("implementation")),
                           "option":"same-client","rps":batch_rps,"avgWallMs":swall,"p50Ms":swall,"p95Ms":swall,"p99Ms":swall,"maxWallMs":swall,
                           "success":1 if s["Success"] else 0,"failed":0 if s["Success"] else 1,"toolCalls":float(s["ToolCalls"]),"logicalOps":float(s["LogicalOps"]),
                           "estimatedInputTokens":ib/4,"estimatedOutputTokens":ob/4,"estimatedContextTokens":pb/4}
                        groups.setdefault((x["implementation"],),[]).append(x)
        for _,v in groups.items():
            x=dict(customers=v[0]["customers"],concurrency=v[0]["concurrency"],implementation=v[0]["implementation"],option=v[0]["option"],samples=len(v))
            for f in ["rps","avgWallMs","p50Ms","p95Ms","p99Ms","toolCalls","logicalOps","estimatedInputTokens","estimatedOutputTokens","estimatedContextTokens"]:
                x[f]=sum(z[f] for z in v)/len(v)
            x["p50Ms"]=pct([z["avgWallMs"] for z in v if z["success"]],.5)
            x["p95Ms"]=pct([z["avgWallMs"] for z in v if z["success"]],.95)
            x["p99Ms"]=pct([z["avgWallMs"] for z in v if z["success"]],.99)
            x["maxWallMs"]=max(z["maxWallMs"] for z in v);x["success"]=sum(z["success"] for z in v);x["failed"]=sum(z["failed"] for z in v);enrich(x);out.append(x)
    return out
for run,kind in [("run5","run5"),("run5b","run5-same-client")]:
    rows=metadata_family(run,kind)
    # Run 5b's published experiment is explicitly a 4 × 4 matrix. An older
    # 1-customer/C1 smoke run exists in the raw artifacts, but it is not part
    # of the declared experiment and must not leak into the public matrix.
    if run=="run5b":
        rows=[x for x in rows if x["customers"] in (10,100,1000,10000) and x["concurrency"] in (8,16,32,64)]
    dump(os.path.join(ASSET,f"{run}-aggregate.json"),{"schemaVersion":3,"run":run,"reportType":"measured-comparison","assumptions":ASS,"aggregate":rows})
# Canonical matrix: Run 4 uses its agent variant; other runs retain their complete aggregate.
matrix={"schemaVersion":1,"generatedUtc":datetime.now(timezone.utc).isoformat(),"runs":{}}
for run in ["run1","run2","run3","run4","run5","run5b"]:
    d=load(os.path.join(ASSET,f"{run}-aggregate.json")); matrix["runs"][run]=[x for x in d["aggregate"] if run!="run4" or x["option"]=="agent"]
dump(os.path.join(ASSET,"benchmark-matrix.json"),matrix)
print("Built unified reports:",", ".join(matrix["runs"].keys()))
