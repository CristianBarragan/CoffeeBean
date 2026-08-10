# Getting Started

The fastest way to understand Foundgine is to run the Banking proof.

## Requirements

- .NET 9 SDK
- a machine capable of running the repository tests
- no external database is required for the canonical sample

## First steps

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project samples/Foundgine.Samples.Banking
```

The repository uses SQLite for the canonical sample so the execution proof is self-contained.

## Read next

- [Installation](Installation.md)
- [First Service](First-Service.md)
- [Configuration](Configuration.md)
- [FAQ](FAQ.md)
