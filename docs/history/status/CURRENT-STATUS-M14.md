# Current Status — M14

M14 proves that the provider boundary is real: SQL and a test-only reference
provider consume the same `ExecutionPlan` produced from the same semantic
request.

The reference provider is deliberately test-only and does not become another
production persistence stack.
