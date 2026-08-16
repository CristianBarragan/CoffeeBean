# M18.15 — Test Fixes

This correction addresses three concrete issues found during the M18.15 test run:

1. Removes the duplicate `ulong` pattern in `SemanticEquivalenceFingerprint`.
2. Prevents `RewriteRuleComposer` from treating an exact executable-plan
   fingerprint no-op as a rewrite cycle.
3. Adds regression coverage for aggregate semantic fingerprint preservation
   and equivalent rebuilt plans.

The fixes are implementation-level corrections; the tests are not weakened.
