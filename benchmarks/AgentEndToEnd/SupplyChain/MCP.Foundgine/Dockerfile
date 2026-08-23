FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore benchmarks/AgentEndToEnd/SupplyChain/MCP.Foundgine/MCP.Foundgine.csproj
RUN dotnet publish benchmarks/AgentEndToEnd/SupplyChain/MCP.Foundgine/MCP.Foundgine.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet","MCP.Foundgine.dll"]
