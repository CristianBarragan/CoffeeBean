# Phase 14 CI Checklist

Run in an environment with .NET 9 SDK installed:

1. `dotnet --info`
2. `dotnet restore Foundgine.sln`
3. `dotnet build Foundgine.sln -c Release --no-restore`
4. `dotnet test Foundgine.sln -c Release --no-build`
5. Build/test benchmark projects separately.
6. `dotnet pack` the intended NuGet projects.
7. Verify package README/license contents.
8. Run security/fuzz tests in isolation.
9. Run MCP end-to-end tests.
10. Run the existing performance suite and compare against the baseline.

Do not mark Phase 14 complete from static inspection alone.
