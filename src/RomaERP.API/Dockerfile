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
# Installs the headless Chromium build (and its OS-level shared library dependencies) used to render
# invoice PDFs — see PlaywrightHtmlToPdfRenderer. Uses the Node runtime the Microsoft.Playwright NuGet
# package already bundles under .playwright/, so no separate Node.js install is needed.
RUN .playwright/node/linux-x64/node .playwright/package/cli.js install --with-deps chromium \
    && rm -rf /var/lib/apt/lists/*
ENTRYPOINT ["dotnet", "RomaERP.API.dll"]
