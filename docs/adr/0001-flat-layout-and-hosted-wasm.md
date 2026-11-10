# ADR 0001: Flat project layout and Api-hosted Blazor WebAssembly

- **Status:** Accepted
- **Date:** 2026-11-11

## Context

The spec (section 11) places production projects under `src/` and lists the Api's
project references as Features, Infrastructure, Application, and Contracts.
It also requires the hosted Blazor WebAssembly model (section 19.1): one origin, one
deployable artifact. The "ASP.NET Core Hosted" template was removed in .NET 8, so hosting
is now done by referencing the client project from the server and adding
`Microsoft.AspNetCore.Components.WebAssembly.Server`.

## Decision

1. Production projects live at the repository root (`SpendScope.Api/`, `SpendScope.Domain/`, ...).
   There is no `src/` folder. Tests stay under `tests/`.
2. `SpendScope.Api` references `SpendScope.Web` so that `dotnet publish SpendScope.Api`
   ships the WASM bundle in its `wwwroot`. `Program.cs` calls `UseBlazorFrameworkFiles()`,
   `UseStaticFiles()`, and `MapFallbackToFile("index.html")`.

## Consequences

- All path-based commands in the spec drop the `src/` prefix, e.g.
  `dotnet ef database update --project SpendScope.Infrastructure --startup-project SpendScope.Api`.
- The Api -> Web reference is build-time only (static assets). ARCH-06 still holds: Web
  must not reference any server project. Api code must not use Web types; an
  architecture test should enforce that.
