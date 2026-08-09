# Foundgine.Serialization (placeholder)

No content extracted yet. Today, JSON handling (Newtonsoft.Json) is only referenced
from the legacy monolith and from `Graphgine.HotChocolate`'s ASP.NET Core wiring —
there isn't yet a protocol-agnostic serialization concern to pull out on its own.

This project exists so the solution shape matches the intended architecture. A
reasonable first candidate to land here: a `Foundgine.Metadata` <-> JSON convention
for caching compiled metadata (`MetadataRegistry`) across process restarts.
