# Execution Receipts

Execution receipts are the durable, provider-neutral record of a Foundgine execution.

A receipt does not copy the request or returned domain data. Instead it records stable fingerprints and execution metadata:

- request identity
- semantic model version
- capability contract version
- intent fingerprint
- authorized plan fingerprint
- authorization fingerprint
- provider
- execution timestamps
- affected semantic nodes
- conservative execution effects
- result fingerprint
- optional approval identity

## Trust boundary

A receipt is evidence, not authorization. Execution authorization is evaluated before execution and, for approved execution, the current authorized plan must still match the approved plan fingerprint.

## Result privacy

The result fingerprint is a SHA-256 digest over a canonicalized representation of returned values and page information. The receipt therefore provides correlation and integrity evidence without retaining the result payload.

## Approval binding

Approved executions carry the approval identifier and approver metadata. The approval remains bound to the exact plan fingerprint and is revalidated immediately before execution.
