# Foundgine SupplyChain Semantic Sample — Guide

This guide is the detailed reference for `samples/Foundgine.SupplyChain.Semantic`. The
[README](README.md) is the short pitch; this document walks through every moving part —
the mixed manual/generated semantic model, the authorization policy, and the client-claims
feature — with enough detail to extend the sample yourself.

## The shape of the boundary

Before any of the implementation detail, here is the path a request takes:

```
Agent / MCP caller
       ↓
Intent
       ↓
Foundgine semantic boundary
       ↓
Authorization
       ↓
Plan
       ↓
Provider
```

Everything in the rest of this guide — domain, semantic model, claims, policy, MCP —
is the sample's implementation of **one step**: the semantic boundary and the
authorization decision that happens at it. Claims validation is a supporting detail of
that boundary, not the headline feature. The headline feature is that every caller,
regardless of transport, goes through the same authorization surface before a plan is
ever built.

## What this sample does NOT implement

This is worth stating explicitly, because it's easy to read the MCP tool code below and
assume it's doing more than it is. The MCP layer in this sample does not implement:

- tenant predicates
- relationship traversal
- authorization predicate composition
- SQL (or any provider) generation
- joins
- mutation planning
- provider-specific execution
- capability semantics

All of that belongs to Foundgine, underneath `SupplyChainAuthorization` and the
semantic model. The application code you'll read in this sample — the MCP tools, the
client-claims validator, the configured policy subclass — is the thin layer that authenticates a
caller, validates the claims it hands over, and then asks Foundgine to authorize and
execute an intent. It is not reimplementing authorization logic; it's *supplying inputs*
to Foundgine's authorization logic.

## Contents

1. [Layout](#layout)
2. [Domain and semantic model](#domain-and-semantic-model)
3. [Manual vs. generated semantics](#manual-vs-generated-semantics)
4. [Identity vs. claims — the core distinction](#identity-vs-claims--the-core-distinction)
5. [Claims validation](#claims-validation)
6. [How the configured policy consumes validated claims](#how-the-policy-consumes-validated-claims)
7. [MCP tool surface](#mcp-tool-surface)
8. [Adversarial MCP client](#adversarial-mcp-client)
9. [Full attack / legitimate-use matrix](#full-attack--legitimate-use-matrix)
10. [Running everything](#running-everything)
11. [Extending the sample](#extending-the-sample)

## Layout

```
samples/Foundgine.SupplyChain.Semantic/
├── Domain/Domain.cs                          domain records + [SemanticEntity]/[SemanticPolicy] annotations
├── Semantics/SupplyChainSemanticModel.cs      manual semantic authoring (SemanticModelBuilder)
├── Semantics/Generated/                       generated semantic authoring, imported into the same model
├── Authorization/
│   ├── FoundgineSemanticAttributes.cs (in src/Foundgine.Semantics)                   the annotation types themselves ([SemanticEntity], [SemanticPolicy], ...)
│   ├── SupplyChainAuthorization.cs       the runtime authorization policy (ISemanticAuthorizationPolicy)
│   └── ClientClaims.cs                        client-claim validator + result types (this feature)
├── Api/
│   ├── Program.cs                              deterministic in-memory scenario runner
│   └── Mcp/Program.cs                          the MCP server exposing SupplyChain over tools/call
├── McpClient/Program.cs                        the Run-5-style adversarial MCP client
└── Tests/
    ├── AuthorizationPolicyTests.cs             policy + claims-validator unit tests
    └── Mcp/AuthorizationMcpPenetrationTests.cs the attack matrix + wire-contract test
```

## Domain and semantic model

The domain is intentionally a mix of manually authored and generated semantics. This
guide focuses on what happens *after* the model exists: authorization. The authoring
API itself is covered next.

## Manual vs. generated semantics

```
Application model
       │
       ├── manual semantics    (Semantics/SupplyChainSemanticModel.cs)
       │
       └── generated semantics (Semantics/Generated/)
                ↓
           AOT metadata
                ↓
         Foundgine runtime
```

Both paths feed the same `SemanticModelBuilder` and end up in the same runtime model —
the sample doesn't distinguish between them at the authorization or execution layer.
The split exists so you can see both authoring styles side by side:

- **Manual** (`SupplyChainSemanticModel.cs`) — semantics authored directly against the
  builder API. Use this when you're hand-tuning a model or exploring the API surface.
- **Generated** (`Semantics/Generated/`) — semantics produced ahead of time from the
  domain's `[SemanticEntity]`/`[SemanticPolicy]` annotations (see `Domain/Domain.cs`),
  compiled into AOT metadata so the runtime never reflects over attributes at startup.

This is one of the more consequential architectural choices in Foundgine: the semantic
model doesn't have to be built dynamically at runtime. It can be generated once, ahead
of time, and loaded as metadata — which is what makes the AOT story possible.

## Identity vs. claims — the core distinction

Before this feature, `SupplyChainAuthorization` was constructed directly from a
server-resolved `(tenantId, role)` pair, and the MCP tools resolved that pair with:

```csharp
private static (string TenantId, SupplyChainRole Role) Authenticate(string actor, string token)
{
    if (!Actors.TryGetValue(actor, out var identity) || !CryptographicEquals(identity.Token, token))
        throw new UnauthorizedAccessException("Invalid actor credentials.");
    return (identity.TenantId, identity.Role);
}
```

That authentication boundary is unchanged. **Identity still only ever comes from
`Authenticate(actor, token)`.** What this feature adds is a second, independent input:
a `claims` dictionary the MCP *caller itself* attaches to a tool call — the kind of
extra context a real client might send alongside a bearer token: "I only need read
access for this call", "scope this to warehouse 12", "here is the change ticket
justifying this write".

The critical design rule, enforced throughout: **a claim can only ever narrow what
the authenticated role already allows. It can never widen it.** Nothing in this
sample lets a claim grant a permission the role doesn't already have. Concretely:

- `scope=read-only` — a manager can tell the server "treat me as read-only for this
  call". A customer cannot use `scope=full` to become writable — there's no such
  effect, because every write check still starts from the role check.
- `warehouse=<id>` — narrows a read to one warehouse by ANDing an extra predicate
  onto whatever predicate the role/tenant already produced. It can never be used to
  see a warehouse outside the caller's tenant, because it is always combined with
  `AND`, never `OR`.
- `reason` / `change_ticket` — required *in addition to* the manager-only role check
  for the `inventory.reconcile` named operation. They add a requirement; they cannot
  remove the role requirement.

And the corollary: **claims that try to assert identity or privilege directly are
never trusted, full stop** — not "trusted if they happen to match reality", not
"trusted with extra scrutiny". A claim named `role`, `tenant`, `tenantId`, `actor`,
`isAdmin`, or `permissions` is rejected outright, and the entire call fails closed,
even if the asserted value happens to be correct. Identity has exactly one source
in this sample: `Authenticate(actor, token)`.

## Claims validation

All of this lives in `Authorization/ClientClaims.cs`, in `ClientClaimsValidator.Validate`.
It treats every incoming claim as hostile until it passes a specific rule, and it never
looks at anything except the raw dictionary the MCP tool was called with — it has no
access to the request's actor/token/role, by design, so it cannot accidentally cross-check
a claim against identity and "average" the two into something more permissive.

```csharp
public static ClaimsValidationResult Validate(
    IReadOnlyDictionary<string, string>? rawClaims,
    DateTimeOffset now)
```

returns

```csharp
public sealed record ClaimsValidationResult(
    IReadOnlyDictionary<string, string> Accepted,
    IReadOnlyList<RejectedClaim> Rejected,
    bool IsSpoofingAttempt);
```

### Validation rules, in order

1. **Reserved identity keys are a hard, whole-request failure.**
   `role`, `tenant`, `tenantId`, `actor`, `isAdmin`, `admin`, `permissions`,
   `capabilities`, `scopes` — if *any* of these appear in the raw claim dictionary,
   `IsSpoofingAttempt` is `true`, `Accepted` is empty, and the caller (the MCP tool
   method) must refuse the entire call rather than process the remaining claims.
   This is deliberately all-or-nothing: a caller who tries this once has shown intent
   to spoof identity, and partially honoring the rest of the call would still leak
   information about what *would* have been allowed.

2. **Recognized non-identity keys are validated against a strict per-key format.**
   Unrecognized-format values are rejected individually — the claim is dropped and
   recorded in `Rejected`, but the rest of the call proceeds without it.

   | Claim | Format | Rejected example |
   |---|---|---|
   | `scope` | `read-only` or `full` | `full-access` |
   | `warehouse` | positive integer | `-3`, `not-a-number` |
   | `max_rows` | integer in `(0, 10000]` | `999999` |
   | `reason` | 8–240 characters | `short` |
   | `change_ticket` | `CHG-####` (4+ digits) | `TICKET-1` |
   | `not_after` | ISO-8601 timestamp | `not-a-date` |

3. **Unrecognized keys are dropped individually, not treated as an attack.**
   A key outside the recognized set (e.g. `favorite_color`) is rejected with reason
   `"Unrecognized claim key; ignored."` and the rest of the call proceeds normally.
   This is the "fail-closed on trust, fail-open on noise" rule: the server never
   silently accepts something it doesn't understand, but it also doesn't treat every
   typo or unrelated field as an intrusion attempt the way it treats an identity key.

4. **Expired evidence is cross-validated and rejected as stale.**
   If `not_after` is present and already in the past relative to `now`, it is removed,
   and so is anything it was meant to bound (`reason`, `change_ticket`) — the intent is
   that a change ticket presented after its own stated validity window can no longer be
   used as evidence, even though each field is individually well-formed.

### Why validation is a separate step from the configured policy

`ClientClaimsValidator` has no knowledge of `SupplyChainRole`, `TenantId`, or the
`SemanticModel`. It only knows the shape of a claim. This means:

- It can be unit tested in complete isolation (see `ClientClaimsValidatorTests`).
- `SupplyChainAuthorization` never sees a raw, unvalidated claim — its constructor
  only accepts a `ClaimsValidationResult`, and it only ever reads `.Accepted`. There is
  no code path in the configured policy that can reach a rejected or malformed claim.

## How the configured policy consumes validated claims

`SupplyChainAuthorization` gained one new configuration factory:

```csharp
public SupplyChainAuthorization(string tenantId, SupplyChainRole role, ClaimsValidationResult validatedClaims)
```

The existing two-argument constructor still exists and delegates to this one with
`ClaimsValidationResult.Empty`, so every pre-existing call site and test keeps working
unmodified.

Three places in the configured policy read `Claims` (the accepted, post-validation dictionary):

- **`SelfRestrictedToReadOnly`** — `Claims["scope"] == "read-only"`. ANDed into
  `CanWriteEntity`, `CanWriteField`, `CanWriteRelationship`. A manager who sends this
  claim cannot write for that call, even though the role alone would allow it.

- **`WarehouseScope`** — `Claims["warehouse"]`. Used in `GetPredicate` to AND an
  extra `resource.WarehouseId == <id>` (or `resource.Id == <id>` for the `Warehouse`
  entity itself) onto whatever predicate the tenant rule already produced. Because
  `AuthorizationDecision`/predicate composition in this sample always uses
  `AuthorizationPredicate.And`, the combined predicate can only be *more* selective
  than the tenant-only predicate, never less.

- **`HasValidEvidence`** — `Claims.ContainsKey("reason") && Claims.ContainsKey("change_ticket")`.
  Checked in the `inventory.reconcile` branch of `GetEntityAccess(entityId, operation, name)`,
  *after* the existing manager-only role check. Missing or malformed evidence denies the
  operation even for a manager; valid evidence never bypasses the role check for anyone else
  (see `Reconcile_requires_manager_role_and_valid_evidence_claims_together` in the tests).

## MCP tool surface

Conceptually, every tool below does the same three things: authenticate the caller,
validate any claims it sent, and hand the resulting identity + claims to Foundgine as
authorization inputs. None of the tools implement authorization decisions themselves —
that stays in `SupplyChainAuthorization` and the Foundgine runtime underneath it.
What's shown here is the translation step: MCP request in, Foundgine call out.

`Api/Mcp/Program.cs` exposes the following tools. `claims` is a new, optional
`Dictionary<string,string>` parameter on the three tools where it's meaningful.

| Tool | New `claims` parameter? | Behavior |
|---|---|---|
| `describe_capabilities` | no | Unchanged — capability discovery for the authenticated identity only. |
| `read_entity` | yes | Claims validated before the read is authorized; response includes `acceptedClaims`/`rejectedClaims`. |
| `read_relationship` | no | Unchanged. |
| `write_entity` | yes | Same claim handling as `read_entity`, applied to the write decision. |
| `policy_probe` | yes | Adds `claims-scope-narrowing`, `claims-warehouse-scoping`, and `claims-reconcile` attack names alongside the original probes. |

Every claims-aware tool follows the same shape:

```csharp
var validatedClaims = ValidateClaims(claims);
if (validatedClaims.IsSpoofingAttempt)
    return ClaimSpoofingError(validatedClaims);
// ... build the configured policy with validatedClaims, proceed as before ...
return WithClaimDiagnostics(result, validatedClaims);
```

`WithClaimDiagnostics` wraps the normal result with `acceptedClaims` and `rejectedClaims`
so the demo (and the adversarial client) can see exactly which claims were honored and why
any others weren't — this is diagnostic information for the sample, not something a
production API would necessarily echo back.

`ClaimSpoofingError` is returned *before* the configured policy or the model is touched at all when an
identity-spoofing claim is present — the request never reaches `Authenticate`'s result being
used together with the claims, because it's rejected purely on the shape of the claim set.

## Adversarial MCP client

`McpClient/Program.cs` extends the existing Run-5-style case list with two groups:

**Attacks** — a caller supplies claims to try to escalate:

- `claims: role injection attempt` — `alice` (Customer) sends `role=SupplyChainManager`
  on a write. Expected: rejected outright (spoofing), not evaluated as a role-check failure.
- `claims: tenant injection attempt` — `analyst-a` (tenant-a) sends `tenant=tenant-b` on a
  cross-tenant probe. Expected: rejected outright.
- `claims: reconcile without evidence` — `manager-a` calls the reconcile probe with no claims.
  Expected: denied (evidence missing).
- `claims: reconcile with malformed change ticket` — evidence present but `change_ticket`
  fails the `CHG-####` format. Expected: denied (the malformed claim never reaches `Accepted`).
- `claims: reconcile with expired evidence` — well-formed evidence, but `not_after` is in
  the past. Expected: denied (evidence expired).

**Legitimate, self-narrowing uses** — a caller supplies claims to *restrict* itself, and the
test confirms the restriction is honored rather than silently ignored:

- `claims: self-imposed read-only scope` — `manager-a` sends `scope=read-only`, then the probe
  checks write access. Expected: denied for *this call*, even though the role allows writes.
- `claims: warehouse scoping narrows predicate` — `operator-a` sends `warehouse=12`. Expected:
  a conditional predicate comes back (tenant AND warehouse), not a flat allow.
- `claims: unknown claim key ignored` — `analyst-a` sends an unrelated `favorite_color` claim
  on an otherwise ordinary cross-tenant probe. Expected: the call proceeds exactly as it would
  without the claim — noise doesn't block a legitimate request.
- `claims: valid reconcile evidence` — `manager-a` sends well-formed `reason` and
  `change_ticket`. Expected: allowed — the positive control for the reconcile path.

The client's `Classify` function was extended with two more buckets: calls that must succeed
cleanly (added `"claims: valid reconcile evidence"` alongside the existing positive controls),
and calls expected to come back conditional rather than flatly denied (added the warehouse-
scoping and unknown-key cases alongside the original cross-tenant case).

## Full attack / legitimate-use matrix

| Case | Actor | Claim(s) sent | Expected result | Why |
|---|---|---|---|---|
| Role injection | `alice` (Customer) | `role=SupplyChainManager` | Rejected, whole call fails closed | Identity claims are never trusted, regardless of role |
| Tenant injection | `analyst-a` (tenant-a) | `tenant=tenant-b` | Rejected, whole call fails closed | Same — tenant is identity, not a claim |
| Missing evidence | `manager-a` | *(none)* | Denied | `inventory.reconcile` requires `reason` + `change_ticket` |
| Malformed evidence | `manager-a` | `change_ticket=not-a-ticket` | Denied | Fails the `CHG-####` format check individually |
| Expired evidence | `manager-a` | valid evidence + past `not_after` | Denied | Cross-field staleness check removes the evidence |
| Self-imposed read-only | `manager-a` | `scope=read-only` | Write denied for this call | Claim narrows a role that would otherwise allow it |
| Warehouse scoping | `operator-a` | `warehouse=12` | Conditional (tenant AND warehouse) | Claim ANDs onto the existing predicate, never ORs |
| Unknown claim | `analyst-a` | `favorite_color=blue` | Normal result, claim dropped | Unrecognized keys are noise, not an attack |
| Valid reconcile | `manager-a` | valid `reason` + `change_ticket` | Allowed | Role check passes AND evidence is well-formed and fresh |

## Running everything

Start the MCP server:

```bash
dotnet run --project samples/Foundgine.SupplyChain.Semantic/Api/Mcp/Foundgine.SupplyChain.Semantic.Mcp.Api.csproj --urls http://localhost:4782
```

Run the adversarial client against it:

```bash
dotnet run --project samples/Foundgine.SupplyChain.Semantic/McpClient/Foundgine.SupplyChain.Semantic.Mcp.Client.csproj
```

Run the unit tests (does not require the server):

```bash
dotnet test samples/Foundgine.SupplyChain.Semantic/Tests/Foundgine.SupplyChain.Semantic.Tests.csproj
```

Set `RUN_SUPPLYCHAIN_MCP_TESTS=1` to additionally opt in to the wire-contract test in
`AuthorizationMcpPenetrationTests` that talks to a running server on `localhost:4782`.

## Extending the sample

A few notes if you add another claim or another named operation:

- **Adding a new recognized claim key**: add it to `RecognizedKeys` and to the `switch` in
  `ValidateFormat` in `ClientClaims.cs`. Decide up front whether it's a *narrowing* claim
  (consumed by the configured policy to restrict something) or an *evidence* claim (required alongside
  a role check for a specific named operation) — the two categories are handled differently
  in `SupplyChainAuthorization` and mixing them tends to blur the "claims only narrow"
  invariant this sample is built around.
- **Adding a new identity-shaped key** (anything that describes *who* the caller is, not
  *what* they're asking for): add it to `ReservedIdentityKeys` instead. When in doubt, a key
  belongs in `ReservedIdentityKeys` if trusting it would let the caller change which role or
  tenant the configured policy evaluates against.
- **Adding a new evidence-gated named operation**: add its name to
  `OperationsRequiringEvidence` in `SupplyChainAuthorization`, and add the role check
  for it before the evidence check — evidence should always be a second gate on top of role,
  never a replacement for it.
- **Testing a new case**: add a unit test in `AuthorizationPolicyTests.cs` (fast, no server),
  a `policy_probe` attack branch in `Api/Mcp/Program.cs` if it needs to be reachable over MCP,
  and a case in `McpClient/Program.cs` plus a row in this guide's matrix so the adversarial
  client and the documentation stay in sync with the configured policy.
