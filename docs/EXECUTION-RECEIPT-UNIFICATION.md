# Execution Receipt Unification

## Purpose

Execution receipts are the canonical semantic evidence contract for both read and mutation execution.

## Invariant

```text
Intent
  -> Authorization
  -> Plan
  -> [optional Approval]
  -> Execution
  -> ExecutionReceipt
```

A mutation must not create a second audit/evidence model.

## Required identity

Every receipt binds:

- semantic model version
- capability contract/version
- intent version
- plan version
- intent fingerprint
- plan fingerprint
- authorization fingerprint
- provider
- execution status

Approved executions additionally bind:

- approval ID
- approver
- approval timestamp

## Security rule

An execution receipt is evidence, not authority. It never grants permission to execute.

Authorization must still be evaluated by the execution path, and approved mutations must verify that the current authorized plan fingerprint matches the approved fingerprint.

## Mutation evidence

Mutation receipts should additionally record:

- affected semantic nodes
- semantic effects
- result fingerprint when available
- provider timing

This keeps read and write execution auditable through one contract while preserving the distinction between authorization and evidence.
