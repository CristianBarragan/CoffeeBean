FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore samples/Foundgine.SupplyChain/Foundgine.SupplyChain.csproj
RUN dotnet publish samples/Foundgine.SupplyChain/Foundgine.SupplyChain.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","Foundgine.SupplyChain.dll"]
