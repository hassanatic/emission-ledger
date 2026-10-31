FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/EmissionLedger/EmissionLedger.csproj src/EmissionLedger/
RUN dotnet restore src/EmissionLedger/EmissionLedger.csproj
COPY src/ src/
RUN dotnet publish src/EmissionLedger/EmissionLedger.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "EmissionLedger.dll"]
