FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore \
    benchmarks/CoffeeBeanery.Performance/HotChocolate.CoffeeBeanery.BenchmarkApi/HotChocolate.CoffeeBeanery.BenchmarkApi.csproj

RUN dotnet publish \
    benchmarks/CoffeeBeanery.Performance/HotChocolate.CoffeeBeanery.BenchmarkApi/HotChocolate.CoffeeBeanery.BenchmarkApi.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "HotChocolate.CoffeeBeanery.BenchmarkApi.dll"]