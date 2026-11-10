# Multi-stage build (spec 25.1). The published Api serves the Blazor WebAssembly client.

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Restore first, from project files only, so this layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY SpendScope.Domain/SpendScope.Domain.csproj SpendScope.Domain/
COPY SpendScope.Application/SpendScope.Application.csproj SpendScope.Application/
COPY SpendScope.Contracts/SpendScope.Contracts.csproj SpendScope.Contracts/
COPY SpendScope.Infrastructure/SpendScope.Infrastructure.csproj SpendScope.Infrastructure/
COPY SpendScope.Features/SpendScope.Features.csproj SpendScope.Features/
COPY SpendScope.Web/SpendScope.Web.csproj SpendScope.Web/
COPY SpendScope.Api/SpendScope.Api.csproj SpendScope.Api/
RUN dotnet restore SpendScope.Api/SpendScope.Api.csproj

COPY . .
RUN dotnet publish SpendScope.Api/SpendScope.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Publishing the hosting Api copies SpendScope.Web's *source* index.html, which still contains the
# build-time placeholders (blazor.webassembly#[.{fingerprint}].js, an empty import map), so the
# client would never start. Publishing Web on its own produces the processed file; fingerprints
# are content hashes, so they match the _framework files already in the Api output.
RUN dotnet restore SpendScope.Web/SpendScope.Web.csproj \
 && dotnet publish SpendScope.Web/SpendScope.Web.csproj -c Release -o /tmp/web --no-restore \
 && cp /tmp/web/wwwroot/index.html /app/publish/wwwroot/index.html

# Guard: fail the image build if a placeholder survives or index.html points at a missing file.
# Checked: src/href attributes and import-map *values* (import-map keys are logical names).
RUN set -e; cd /app/publish/wwwroot; \
    if grep -q '#\[' index.html; then echo "index.html still has asset placeholders" >&2; exit 1; fi; \
    refs=$( { grep -o '\(src\|href\)="_framework/[^"]*"' index.html | sed 's/^[a-z]*="//; s/"$//'; \
              grep -o '": "\./_framework/[^"]*"' index.html | sed 's/^": "\.\///; s/"$//'; } | sort -u ); \
    [ -n "$refs" ] || { echo "index.html has no _framework references" >&2; exit 1; }; \
    for f in $refs; do [ -f "$f" ] || { echo "index.html references missing $f" >&2; exit 1; }; done; \
    echo "index.html OK: $(echo $refs | wc -w) framework references verified"

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The aspnet image ships a non-root "app" user (UID 1654).
USER app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# Platforms such as Render choose the port through $PORT; default to 8080 elsewhere.
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet SpendScope.Api.dll"]
