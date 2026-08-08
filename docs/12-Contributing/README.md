[Home](../../README.md) → [Documentation](../README.md) → **Contributing**

# Contributing

## Contents

- [Code Style](Code-Style.md)
- [Testing](Testing.md)
- [ADR Process](ADR-Process.md)

---

## Philosophy

## Philosophy

CoffeeBeanery is built around a few simple principles.

- Compile-time first
- Immutable by default
- Native AOT friendly
- Transport agnostic
- Dependency inversion
- Single responsibility
- Explicit architecture

Every contribution should reinforce these principles.

---

## Before Contributing

## Before Contributing

Before opening a Pull Request, contributors should read:

- Architecture.md
- Foundation.md
- Runtime.md
- SQL.md
- Generator.md
- Planning.md
- ADR.md

Understanding the architecture is far more important than understanding individual implementations.

---

## Development Workflow

## Development Workflow

Recommended workflow:

```
Fork Repository

↓

Create Branch

↓

Implement Feature

↓

Run Tests

↓

Run Generator Tests

↓

Open Pull Request

↓

Review

↓

Merge
```

Every Pull Request should focus on one logical change.

---

## Pull Requests

## Pull Requests

A good Pull Request should:

- Solve one problem
- Include tests
- Preserve architecture
- Keep commits focused
- Explain architectural impact

Large unrelated changes should be split into multiple PRs.

---

## Review Checklist

Before approving a Pull Request, reviewers should verify:

- Correct dependency direction
- No runtime reflection
- No architectural boundary violations
- Tests updated
- Documentation updated
- Generated output remains deterministic
- Native AOT compatibility preserved

---

---

## Related Documentation

- [Code Style](Code-Style.md)
- [Testing](Testing.md)
- [Reference → ADRs](../13-Reference/ADRs.md)

---

← Previous: [Samples](../11-Samples/README.md)  |  Next: [Reference](../13-Reference/README.md) →
