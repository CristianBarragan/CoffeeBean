# Graphgine.AspNetCore (placeholder)

No content extracted yet. Today, all ASP.NET Core startup wiring (service
registration, `app.MapGraphQL()`, connection-string / RDS auth setup, etc.) lives
directly in the sample's `Program.cs` (`samples/Graphgine.Samples.Banking`).

The natural extraction is a `services.AddGraphgine(...)` / `app.MapGraphgine(...)`
pair of extension methods here, so a consuming app doesn't need to know about
`Graphgine.HotChocolate` wiring details directly. Left as a placeholder rather than
guessed at, since inventing that API surface without a second real consumer risks
getting the shape wrong.
