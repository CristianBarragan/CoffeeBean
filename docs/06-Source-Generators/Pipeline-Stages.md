# Pipeline Stages

> **Historical / future direction.**

This page describes the previous Graphgine generator direction.

The current active repository does not use that generator.

The future compiler direction is documented in [Source Generators](README.md).

The important architectural decision is:

```text
Compile time:
    discover and generate the application's legal semantic vocabulary

Runtime:
    resolve dynamic intent and generate the appropriate execution plan
```

Do not conflate the two.
