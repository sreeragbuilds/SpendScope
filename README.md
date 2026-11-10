# SpendScope

*See where every dollar goes.* A personal expense tracker with analytics.

ASP.NET Core (.NET 10 LTS) API with vertical feature slices, a Blazor WebAssembly client
hosted by the API, and PostgreSQL 16. The full specification is in
[docs/SpendScope_Project_Spec.md](docs/SpendScope_Project_Spec.md).

## Repository layout

```
SpendScope/
├── SpendScope.sln
├── Directory.Build.props        # shared settings: net10.0, nullable, analyzers, warnings as errors
├── Directory.Packages.props     # central package versions
├── global.json                  # pins the .NET 10 SDK
├── docker-compose.yml           # PostgreSQL, OpenTelemetry collector, Jaeger
├── SpendScope.Domain/           # entities, value objects, invariants (no dependencies)
├── SpendScope.Application/      # abstractions, Result<T>, behaviors        -> Domain
├── SpendScope.Contracts/        # DTOs shared by API and client             (no dependencies)
├── SpendScope.Infrastructure/   # EF Core, Identity, tokens, clock, email   -> Application, Domain
├── SpendScope.Features/         # vertical slices                           -> Application, Domain, Contracts
├── SpendScope.Api/              # composition root; hosts the WASM client   -> Features, Infrastructure, Application, Contracts, Web
├── SpendScope.Web/              # Blazor WebAssembly client                 -> Contracts
├── tests/
│   ├── SpendScope.UnitTests/
│   ├── SpendScope.IntegrationTests/
│   ├── SpendScope.ArchitectureTests/
│   └── SpendScope.Web.Tests/
└── docs/
    ├── adr/                     # architecture decision records
    ├── api/                     # exported OpenAPI json
    └── observability/otel.yaml  # local collector config
```

Projects sit at the repository root, not under `src/` (see
[ADR 0001](docs/adr/0001-flat-layout-and-hosted-wasm.md)).

## Prerequisites

- .NET SDK 10.0.201 or later 10.0.x (`dotnet --version`)
- Docker Desktop (`docker --version`)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

## Getting started

```bash
# 1. Start PostgreSQL (add otel-collector and jaeger for tracing; Jaeger UI: http://localhost:16686)
docker compose up -d postgres

# 2. One-time: store the connection string as a user secret (never in appsettings)
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=spendscope;Username=spendscope;Password=localdev" --project SpendScope.Api

# 3. Build, test, run
dotnet build
dotnet test
dotnet run --project SpendScope.Api --launch-profile https
```

The app is served at https://localhost:7000. The Api refuses to start if
`ConnectionStrings:Default` is missing. Outside Development, set it with the
`ConnectionStrings__Default` environment variable.

`dotnet test` needs Docker running: the integration tests start their own PostgreSQL 16
container with Testcontainers.

## Health checks

| Endpoint | Meaning | Unhealthy response |
|---|---|---|
| `GET /health/live` | The process is up. Runs no checks. | never (no response means the process is down) |
| `GET /health/ready` | PostgreSQL is reachable (3 s timeout). | `503` |

Both are anonymous and return a small JSON body (`status`, per-check `name`/`status`/`durationMs`).
Exception details go to the logs only.

## Logging

Serilog: readable console output in Development, compact JSON on the console elsewhere. Levels
are set in the `Serilog:MinimumLevel` section of `appsettings*.json`. Each request is logged
once with method, path (never the query string), status, elapsed time and user ID. Healthy
`/health` probes are logged at Debug so they don't flood the logs.

## Architecture tests

`tests/SpendScope.ArchitectureTests` enforces ARCH-01 to ARCH-09 from the spec on every
`dotnet test`. Where the spec was ambiguous, the interpretation is recorded in
[ADR 0002](docs/adr/0002-architecture-rule-interpretations.md).

## Conventions

- Package versions are declared only in `Directory.Packages.props`. Add a package with
  `<PackageReference Include="Name" />` in the project and a `<PackageVersion>` entry centrally.
- Warnings are errors. Code style from `.editorconfig` is enforced during build.
- Conventional commits: `feat(expenses): add CSV export`, `fix(auth): revoke tokens on reuse`.
