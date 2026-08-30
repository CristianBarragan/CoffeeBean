# Migration

Foundgine is a ground-up rebuild. The archived V1 and Graphgine projects are references, not compatibility targets.

When moving code from an archive:

1. identify the capability you actually need;
2. map it to the current semantic contracts;
3. reimplement it in the current layer;
4. add a focused test;
5. delete the old compatibility code.

Do not copy the old project structure into the new repository.

---

That's the full sequence. Back to the [documentation index](README.md).
