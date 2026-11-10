# ADR 0003: Decisions made while implementing Milestones 1 to 8

- **Status:** Accepted
- **Date:** 2026-11-11

## Context

Building the auth, expense, category, report and admin slices and the Blazor client surfaced
places where the spec contradicts itself, leaves a gap, or names a dependency that turned out to
be unsafe. Each decision below records what was done and why.

## Decisions

| # | Topic | Decision | Why |
|---|---|---|---|
| 1 | `ApplicationUser` | Lives in `Infrastructure/Identity`, not `Domain/Users` (spec 11). Slices reach users through `IUserAccounts` in Application. | It derives from `IdentityUser<Guid>`; ARCH-01 forbids framework types in Domain. |
| 2 | Request DTOs | Request records live in **Contracts** (not in each slice). | The Blazor client must send them, and Web may only reference Contracts (ARCH-06). Spec 10.2 already describes Contracts as "requests and responses". |
| 3 | Slice files | One file per slice (`Expenses/CreateExpense/CreateExpense.cs`) holding validator, handler and endpoint. | Spec 10.4 allows "five files at most"; one file keeps each use case readable top to bottom. Split later if a slice grows. |
| 4 | `ToHttpResult` | In `Features/Common/ResultHttp.cs`, not `Api/Endpoints` (appendix A.3). | Endpoints in Features call it, and Features may not reference Api. |
| 5 | Validation | A generic `ValidationFilter<T>` endpoint filter runs the FluentValidation validator; 400 bodies carry `errors` (messages) **and** `errorCodes` (machine codes) per camelCase field. | One mechanism for all slices; clients can switch on codes without parsing messages. |
| 6 | Default date range | When `from`/`to` are omitted, the range is the **whole current month**, not "1st to today" (spec 14.4). | "Today" on the server is a UTC date; for users east of UTC it cut off expenses dated today, and future-dated expenses are allowed. Found by the API smoke test. The client always sends explicit local dates. |
| 7 | Admin aggregates | User-defined categories are combined into one "Custom categories" bucket. | Their names are private data (FR-ADM-04). System category names are shown. |
| 8 | Roles seeding | Roles are seeded by **migration** (`HasData`), not at startup. | Production never auto-migrates (spec 25.4) but still needs the roles. |
| 9 | Email | `ConsoleEmailSender` (logs the reset link) only in Development/Testing. Elsewhere a sender that logs a warning without the body. | Reset tokens must never reach production logs; the app still starts before SMTP exists. |
| 10 | Reset links | Built from `App:PublicBaseUrl`, never the request `Host` header. | Host header injection would let an attacker point reset links at their own site. |
| 11 | Refresh races | Rotation uses a conditional `ExecuteUpdate` (`WHERE RevokedAtUtc IS NULL`) inside a transaction; the client funnels every refresh, including session restore, through one single-flight task. | Two concurrent refreshes with one token must not both succeed, and the client must not trip the server's reuse detection on itself. |
| 12 | JWT claims | Tokens carry `sub`, `name`, `email`, `role`, `jti`; the Api validates with `MapInboundClaims = false` and those names as name/role claim types. | Short, stable claim names used identically by issuer, validator, `ICurrentUser` and the client. |
| 13 | Package pins | `Microsoft.IdentityModel.JsonWebTokens` pinned to 8.19.2, the version JwtBearer 10.0.15 is built against. | Mixed IdentityModel versions are a known cause of signature-validation failures. |
| 14 | bUnit | **bUnit 2.x**, not 1.x (spec 12). | bUnit 1.40 depends on AngleSharp 1.2.0 (GHSA-pgww-w46g-26qg, fixed in 1.5.0) and is binary-incompatible with patched AngleSharp. bUnit 2 targets .NET 10 and ships the fix. |
| 15 | Test frameworks | **NUnit** for `SpendScope.UnitTests` (team preference); xUnit stays for integration, architecture and component tests. Chosen per project in `tests/Directory.Build.props`. | Unit tests are where the preference was stated; the others already used xUnit and WebApplicationFactory/Testcontainers examples. |
| 16 | Chart.js | Loaded from jsDelivr at 4.4.1 with a Subresource Integrity hash. | The browser refuses the file if the CDN ever serves different bytes. |

## Not yet implemented (later milestones)

- **Milestone 9 (hardening):** correlation ID middleware, security headers and CSP,
  `xmin` concurrency tokens with `If-Match`, response caching for `GET /api/categories`.
- **Milestone 10 (delivery):** Dockerfile, production compose, GitHub Actions, migration bundle.
- Multi-currency reporting: v1 sums amounts as recorded. A user who changes their default
  currency will see old and new expenses summed together.
