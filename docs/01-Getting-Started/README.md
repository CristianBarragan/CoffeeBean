[Home](../../README.md) → [Documentation](../README.md) → **Getting Started**

# Getting Started

This section gets a Coffee Beanery-backed service running locally and explains the moving
parts you just started. If you want the philosophy first, see
[Architecture → Vision](../02-Architecture/Vision.md).

---

## Contents

- [Installation](Installation.md) — prerequisites, PostgreSQL + Apache AGE setup, cloning the repo
- [First Service](First-Service.md) — running the sample and understanding the request path
- [Configuration](Configuration.md) — connection strings, DI registration, appsettings
- [FAQ](FAQ.md) — the questions people ask in the first hour

---

## The shortest possible path

```bash
git clone https://github.com/coffee-beanery/coffee-beanery.git
cd coffee-beanery/example/HotChocolateCoffeeBeanery
dotnet build
dotnet run --project Api/Api.Banking
```

That assumes PostgreSQL with Apache AGE is already reachable at the connection string in
`appsettings.json`. If it isn't yet, start with [Installation](Installation.md).

---

## Related Documentation

- [Architecture](../02-Architecture/README.md)
- [Samples](../11-Samples/README.md)
- [Reference → FAQ](../13-Reference/FAQ.md)

---

← Previous: [Documentation Home](../README.md)  |  Next: [Architecture](../02-Architecture/README.md) →
