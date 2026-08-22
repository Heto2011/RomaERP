FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/RomaERP.Domain/RomaERP.Domain.csproj src/RomaERP.Domain/
COPY src/RomaERP.Application/RomaERP.Application.csproj src/RomaERP.Application/
COPY src/RomaERP.Infrastructure/RomaERP.Infrastructure.csproj src/RomaERP.Infrastructure/
COPY src/RomaERP.API/RomaERP.API.csproj src/RomaERP.API/
RUN dotnet restore src/RomaERP.API/RomaERP.API.csproj

COPY src/ src/
RUN dotnet publish src/RomaERP.API/RomaERP.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RomaERP.API.dll"]
