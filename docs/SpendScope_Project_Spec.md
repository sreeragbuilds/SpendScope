# SpendScope: Project Specification

> **Tagline:** *See where every dollar goes.*

| Field | Value |
|---|---|
| Project name | SpendScope |
| Type | Personal expense tracker with analytics |
| Backend | ASP.NET Core (.NET 10 LTS), Layered + Vertical Slice architecture |
| Frontend | Blazor WebAssembly (hosted with the API) |
| Database | PostgreSQL 16 (via Npgsql) |
| Auth | ASP.NET Core Identity + JWT access tokens + refresh tokens |
| Charts | Chart.js 4.4.1 via JS interop |
| Document version | 1.0 |

---

## Table of contents

1. [Vision and goals](#1-vision-and-goals)
2. [Scope](#2-scope)
3. [Glossary](#3-glossary)
4. [Personas](#4-personas)
5. [Functional requirements](#5-functional-requirements)
6. [Non-functional requirements](#6-non-functional-requirements)
7. [Roles and permissions](#7-roles-and-permissions)
8. [Domain model](#8-domain-model)
9. [Database design](#9-database-design)
10. [Architecture](#10-architecture)
11. [Solution structure](#11-solution-structure)
12. [Technology stack](#12-technology-stack)
13. [Authentication and authorization design](#13-authentication-and-authorization-design)
14. [API design](#14-api-design)
15. [Feature slice specifications](#15-feature-slice-specifications)
16. [Reports and aggregation](#16-reports-and-aggregation)
17. [Validation rules](#17-validation-rules)
18. [Error handling](#18-error-handling)
19. [Blazor frontend design](#19-blazor-frontend-design)
20. [Logging and observability](#20-logging-and-observability)
21. [Security checklist](#21-security-checklist)
22. [Configuration](#22-configuration)
23. [Testing strategy](#23-testing-strategy)
24. [Local development setup](#24-local-development-setup)
25. [Containerization and deployment](#25-containerization-and-deployment)
26. [CI/CD pipeline](#26-cicd-pipeline)
27. [Milestones and task list](#27-milestones-and-task-list)
28. [Definition of done](#28-definition-of-done)
29. [Risks and mitigations](#29-risks-and-mitigations)
30. [Future enhancements](#30-future-enhancements)
31. [Interview talking points](#31-interview-talking-points)
32. [Appendix A: Code reference](#appendix-a-code-reference)

---

## 1. Vision and goals

### 1.1 Problem statement
People lose track of small recurring expenses. They can't easily answer "where did my money go last month?" SpendScope lets a user record expenses quickly and see clear, category-level breakdowns and trends.

### 1.2 Learning goals
This project is designed to demonstrate:

- ASP.NET Core Identity, JWT bearer auth, refresh token rotation
- Role-based and resource-based authorization
- Per-user data isolation (defense in depth)
- Layered architecture with vertical feature slices
- Architecture tests that enforce dependency rules
- EF Core: migrations, global query filters, database-side aggregation
- Blazor WebAssembly with typed HTTP clients, auth state, and JS interop for charts
- Integration testing with `WebApplicationFactory` and Testcontainers
- Docker, CI/CD with GitHub Actions

### 1.3 Success criteria
- A user can register, log in, record an expense, and see a category chart within 60 seconds of first use.
- No user can read, change, or delete another user's data, verified by automated tests.
- All architecture rules are enforced by a failing build when broken.
- The category summary for a 12-month range returns in under 300 ms on 100,000 expense rows (measured locally).

---

## 2. Scope

### 2.1 In scope (v1)
- Account registration, login, logout, refresh, password change, password reset
- Expense CRUD for the signed-in user
- User-defined and system-default categories
- Filtering, sorting, and pagination of expenses
- Dashboard with a category doughnut chart and a monthly bar chart
- Date range selection that refreshes charts
- Admin area: list users, lock or unlock accounts, view system-wide category totals
- CSV export of the user's own expenses

### 2.2 Out of scope (v1)
- Multiple currencies and exchange rates (single currency per user, set in profile)
- Bank account syncing or receipt OCR
- Budgets and alerts (planned for v2, see section 30)
- Shared or family accounts
- Mobile native apps
- Social login (planned as an extension)

---

## 3. Glossary

| Term | Meaning |
|---|---|
| **Expense** | A single spending record: amount, date, category, optional note. |
| **Category** | A label for expenses. Either a system category (owned by nobody, visible to all) or a user category (owned by one user). |
| **Owner** | The user whose ID is stored on a record. Only the owner can read or change it. |
| **Access token** | Short-lived JWT (15 min) sent on every API call. |
| **Refresh token** | Long-lived opaque token (14 days) used only to obtain new access tokens. Stored hashed in the database. |
| **Slice** | One feature folder containing its request, validation, handler, response, and endpoint. |
| **IDOR** | Insecure Direct Object Reference: accessing another user's record by changing an ID. |
| **Minor units** | Not used in v1. Money is stored as `numeric(18,2)` in PostgreSQL. |

---

## 4. Personas

**P1: Priya, the everyday saver.** Records 5 to 10 expenses a week on her phone browser. Wants to know her top three spending categories this month.

**P2: Marcus, the budget planner.** Records expenses in bulk once a week. Wants to compare monthly totals over the past year and export to a spreadsheet.

**P3: Dana, the administrator.** Manages accounts and checks overall system health. Must not see any individual user's expense details.

---

## 5. Functional requirements

Requirement IDs are used in the test plan (section 23) so each requirement can be traced to tests.

### 5.1 Account management

| ID | Requirement | Priority |
|---|---|---|
| FR-AUTH-01 | A visitor can register with email, password, and display name. | Must |
| FR-AUTH-02 | Email must be unique (case-insensitive). | Must |
| FR-AUTH-03 | A registered user receives the `User` role automatically. | Must |
| FR-AUTH-04 | A user can log in with email and password and receive an access token and refresh token. | Must |
| FR-AUTH-05 | After 5 failed login attempts, the account is locked for 15 minutes. | Must |
| FR-AUTH-06 | A user can refresh an access token using a valid refresh token. Each refresh rotates the refresh token. | Must |
| FR-AUTH-07 | A user can log out, which revokes the current refresh token. | Must |
| FR-AUTH-08 | A user can change their password by providing the current password. | Must |
| FR-AUTH-09 | A user can request a password reset email. The response is identical whether or not the email exists. | Should |
| FR-AUTH-10 | A user can view and update their display name and default currency. | Must |

### 5.2 Expenses

| ID | Requirement | Priority |
|---|---|---|
| FR-EXP-01 | A user can create an expense with amount, date, category, and optional note. | Must |
| FR-EXP-02 | Amount must be greater than 0 and no more than 1,000,000.00. | Must |
| FR-EXP-03 | Date can be today or in the past, but not more than 1 year in the future. | Must |
| FR-EXP-04 | A user can list their expenses with paging (default 20, max 100 per page). | Must |
| FR-EXP-05 | A user can filter expenses by date range, category, and minimum or maximum amount. | Must |
| FR-EXP-06 | A user can sort by date (default, newest first), amount, or category. | Should |
| FR-EXP-07 | A user can view one of their expenses by ID. | Must |
| FR-EXP-08 | A user can update any field of their own expense. | Must |
| FR-EXP-09 | A user can delete their own expense. Deletion is a hard delete in v1. | Must |
| FR-EXP-10 | Requesting another user's expense returns 404. | Must |
| FR-EXP-11 | A user can export their filtered expenses as CSV. | Should |

### 5.3 Categories

| ID | Requirement | Priority |
|---|---|---|
| FR-CAT-01 | The system provides 10 default categories visible to all users (see section 9.4). | Must |
| FR-CAT-02 | A user can create a personal category with a name and a hex color. | Must |
| FR-CAT-03 | Category names are unique per user, case-insensitive. A user may reuse a system category name only if they do not already see that name. | Must |
| FR-CAT-04 | A user can rename or recolor their own category. | Must |
| FR-CAT-05 | A user can delete their own category. If expenses use it, the request must include a replacement category ID, and those expenses are moved to the replacement. | Must |
| FR-CAT-06 | System categories cannot be edited or deleted by any user, including admins. | Must |

### 5.4 Dashboard and reports

| ID | Requirement | Priority |
|---|---|---|
| FR-RPT-01 | The dashboard shows total spend, expense count, and average expense for a selected date range. | Must |
| FR-RPT-02 | The dashboard shows a doughnut chart of spend by category for the selected range. | Must |
| FR-RPT-03 | The dashboard shows a bar chart of monthly totals for a selected year. | Must |
| FR-RPT-04 | Each category in the summary shows its total, percentage of the grand total, and expense count. | Must |
| FR-RPT-05 | Changing the date range updates every chart and card on the page. | Must |
| FR-RPT-06 | Empty ranges show an empty-state message, not an error. | Must |

### 5.5 Admin

| ID | Requirement | Priority |
|---|---|---|
| FR-ADM-01 | An admin can list all users with paging, search by email, and their status (active, locked). | Must |
| FR-ADM-02 | An admin can lock or unlock a user account. | Must |
| FR-ADM-03 | An admin can view system-wide category totals and monthly totals. These are aggregates only. | Must |
| FR-ADM-04 | An admin cannot view any individual user's expense rows. | Must |
| FR-ADM-05 | An admin cannot lock their own account. | Must |

---

## 6. Non-functional requirements

| ID | Category | Requirement |
|---|---|---|
| NFR-01 | Performance | P95 API latency under 300 ms for list and summary endpoints at 100k rows per user dataset, measured locally. |
| NFR-02 | Performance | Dashboard first meaningful paint under 2 seconds on a mid-range laptop after the WASM bundle is cached. |
| NFR-03 | Security | All endpoints except register, login, refresh, and forgot-password require a valid access token. |
| NFR-04 | Security | Passwords hashed with ASP.NET Core Identity defaults (PBKDF2 with HMAC-SHA256 in .NET 10). |
| NFR-05 | Security | Refresh tokens stored as SHA-256 hashes. Plain tokens are never persisted. |
| NFR-06 | Security | HTTPS enforced in all non-development environments. HSTS enabled. |
| NFR-07 | Security | Security headers: `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Content-Security-Policy`, `Referrer-Policy`. |
| NFR-08 | Reliability | Database writes are transactional. A failed refresh-token rotation does not leave orphaned tokens. |
| NFR-09 | Maintainability | Architecture tests must pass on every build. |
| NFR-10 | Maintainability | Code coverage for the Application and Features projects at or above 75%. |
| NFR-11 | Observability | Every request has a correlation ID returned in the `X-Correlation-Id` header. |
| NFR-12 | Accessibility | Forms have labels, charts have text summaries, color is never the only indicator, and the UI passes basic WCAG 2.1 AA contrast checks. |
| NFR-13 | Portability | The application runs with a single `docker compose up` command. |
| NFR-14 | Usability | Expense entry takes no more than 4 fields and 1 submit action for the common case. |

---

## 7. Roles and permissions

### 7.1 Roles

| Role | Assigned to | Purpose |
|---|---|---|
| `User` | Every registered account by default | Manage own expenses, categories, and profile. |
| `Admin` | Assigned manually via seed data or an admin command | Manage accounts and see aggregates. |

### 7.2 Permission matrix

| Action | Anonymous | User (own data) | Admin |
|---|:-:|:-:|:-:|
| Register, login, refresh, forgot password | ✔ | ✔ | ✔ |
| View own profile, change password | ✘ | ✔ | ✔ |
| CRUD own expenses | ✘ | ✔ | ✘ (no access to expense rows through admin endpoints) |
| Read another user's expense | ✘ | ✘ | ✘ |
| CRUD own categories | ✘ | ✔ | ✔ (own categories only) |
| Edit or delete system categories | ✘ | ✘ | ✘ |
| View own reports | ✘ | ✔ | ✔ (own data) |
| List users, lock or unlock accounts | ✘ | ✘ | ✔ |
| View system-wide aggregated totals | ✘ | ✘ | ✔ |

> **Note:** An admin is also a regular user for their own expenses. Admin endpoints are a separate surface and never return per-user expense rows.

### 7.3 Authorization policies

| Policy name | Requirement |
|---|---|
| `RequireUser` | Authenticated and in role `User` or `Admin`. |
| `RequireAdmin` | Authenticated and in role `Admin`. |
| `ExpenseOwner` | Resource-based: `expense.UserId == current user ID`. |
| `CategoryOwner` | Resource-based: category is system-owned (read only) or belongs to current user. |

---

## 8. Domain model

### 8.1 Entities

#### `ApplicationUser` (extends `IdentityUser<Guid>`)

| Property | Type | Rules |
|---|---|---|
| Id | `Guid` | Primary key, generated by Identity. |
| Email | `string` | Required, max 256, unique. |
| UserName | `string` | Set equal to Email. |
| DisplayName | `string` | Required, 2 to 50 characters. |
| DefaultCurrency | `string` | ISO 4217 code, 3 uppercase letters. Default `USD`. |
| IsLocked | derived | True if `LockoutEnd` is in the future. |
| CreatedAtUtc | `DateTime` | Set on creation. |

#### `Expense`

| Property | Type | Rules |
|---|---|---|
| Id | `Guid` | Primary key, generated with `Guid.CreateVersion7()` (time-ordered). |
| UserId | `Guid` | Required. FK to `AspNetUsers`. Set from the token, never from the client. |
| Amount | `decimal` | Required. Greater than 0. Max 1,000,000.00. Two decimal places. |
| Currency | `string` | 3 uppercase letters. Copied from the user's default when created. |
| OccurredOn | `DateOnly` | Required. Not more than 1 year in the future. |
| CategoryId | `Guid` | Required. FK to `Categories`. |
| Note | `string?` | Optional. Max 500 characters. |
| CreatedAtUtc | `DateTime` | Set on creation. |
| UpdatedAtUtc | `DateTime?` | Set on update. |

**Domain invariants (enforced in the `Expense` class):**
- `Amount` must be positive. The constructor and `ChangeAmount` method throw `DomainException` otherwise.
- `OccurredOn` must not exceed `today + 365 days`. The clock is injected through `IClock`, never `DateTime.Now`, so tests are deterministic.

#### `Category`

| Property | Type | Rules |
|---|---|---|
| Id | `Guid` | Primary key. |
| UserId | `Guid?` | Null for system categories. Set for user categories. |
| Name | `string` | Required, 1 to 40 characters, trimmed. |
| NormalizedName | `string` | Uppercase invariant form of Name, used for uniqueness. |
| ColorHex | `string` | Format `#RRGGBB`. |
| IsSystem | `bool` | True for system categories. Read-only. |
| CreatedAtUtc | `DateTime` | Set on creation. |

#### `RefreshToken`

| Property | Type | Rules |
|---|---|---|
| Id | `Guid` | Primary key. |
| UserId | `Guid` | FK to `AspNetUsers`. |
| TokenHash | `string` | SHA-256 hash of the token, Base64 encoded. Unique. |
| ExpiresAtUtc | `DateTime` | Now + 14 days at creation. |
| CreatedAtUtc | `DateTime` | |
| RevokedAtUtc | `DateTime?` | Set on logout or rotation. |
| ReplacedByTokenHash | `string?` | Set when rotated, used for reuse detection. |
| CreatedByIp | `string?` | Optional, for audit. |

### 8.2 Value objects

| Type | Purpose |
|---|---|
| `Money` | Pairs `decimal Amount` with `string Currency`. Equality by both fields. Prevents mixing currencies in one sum. |
| `EmailAddress` | Validates format on construction. |
| `HexColor` | Validates `#RRGGBB` on construction. |

### 8.3 Domain events (optional, v1 stretch)

| Event | Raised by | Consumed by |
|---|---|---|
| `ExpenseCreated` | `Expense` constructor | Audit log slice (future) |
| `CategoryDeleted` | Category deletion | Expense reassignment (handled inline in v1) |

### 8.4 Default system categories

| Name | Color |
|---|---|
| Food and Dining | `#F59E0B` |
| Groceries | `#84CC16` |
| Transport | `#3B82F6` |
| Housing | `#8B5CF6` |
| Utilities | `#06B6D4` |
| Health | `#EF4444` |
| Entertainment | `#EC4899` |
| Shopping | `#F97316` |
| Education | `#14B8A6` |
| Other | `#6B7280` |

---

## 9. Database design

### 9.1 Engine
PostgreSQL 16. Chosen because `numeric` aggregation works reliably with EF Core and Npgsql. SQLite is not used because its `decimal` support breaks `Sum` translation in EF Core.

### 9.2 Tables

Identity creates the `AspNet*` tables. SpendScope adds the following.

**`Expenses`**

| Column | Type | Nullable | Notes |
|---|---|:-:|---|
| `Id` | `uuid` | No | PK |
| `UserId` | `uuid` | No | FK → `AspNetUsers.Id`, ON DELETE CASCADE |
| `Amount` | `numeric(18,2)` | No | CHECK (`Amount > 0`) |
| `Currency` | `char(3)` | No | |
| `OccurredOn` | `date` | No | |
| `CategoryId` | `uuid` | No | FK → `Categories.Id`, ON DELETE RESTRICT |
| `Note` | `varchar(500)` | Yes | |
| `CreatedAtUtc` | `timestamptz` | No | |
| `UpdatedAtUtc` | `timestamptz` | Yes | |

**Indexes:**
- `IX_Expenses_UserId_OccurredOn` on (`UserId`, `OccurredOn` DESC). Serves the main list and date-range queries.
- `IX_Expenses_UserId_CategoryId_OccurredOn` on (`UserId`, `CategoryId`, `OccurredOn`). Serves category reports.

**`Categories`**

| Column | Type | Nullable | Notes |
|---|---|:-:|---|
| `Id` | `uuid` | No | PK |
| `UserId` | `uuid` | Yes | FK → `AspNetUsers.Id`, ON DELETE CASCADE. Null means system. |
| `Name` | `varchar(40)` | No | |
| `NormalizedName` | `varchar(40)` | No | |
| `ColorHex` | `char(7)` | No | CHECK (`ColorHex ~ '^#[0-9A-Fa-f]{6}$'`) |
| `IsSystem` | `boolean` | No | |
| `CreatedAtUtc` | `timestamptz` | No | |

**Indexes and constraints:**
- Unique index on (`UserId`, `NormalizedName`) where `UserId` is not null.
- Unique index on (`NormalizedName`) where `UserId` is null (system names are globally unique).
- CHECK (`IsSystem` = (`UserId` IS NULL)). Keeps system and user categories consistent.

**`RefreshTokens`**

| Column | Type | Nullable | Notes |
|---|---|:-:|---|
| `Id` | `uuid` | No | PK |
| `UserId` | `uuid` | No | FK, ON DELETE CASCADE |
| `TokenHash` | `varchar(88)` | No | Unique index |
| `ExpiresAtUtc` | `timestamptz` | No | |
| `CreatedAtUtc` | `timestamptz` | No | |
| `RevokedAtUtc` | `timestamptz` | Yes | |
| `ReplacedByTokenHash` | `varchar(88)` | Yes | |
| `CreatedByIp` | `varchar(45)` | Yes | Supports IPv6 length |

**Index:** (`UserId`, `RevokedAtUtc`) for revoking all of a user's tokens.

### 9.3 Seed data

| Seed | When | Content |
|---|---|---|
| Roles | Startup, if missing | `User`, `Admin` |
| System categories | Migration | Ten rows from section 8.4 |
| Admin account | Development only, from user secrets | Email and password from `Seed:Admin:Email` and `Seed:Admin:Password` |

### 9.4 Migrations policy
- One migration per feature change, named descriptively (`AddCategoriesTable`, not `Migration1`).
- Never edit an applied migration. Add a new one.
- Run `dotnet ef migrations script --idempotent` for production deployments.

---

## 10. Architecture

### 10.1 Overview

SpendScope combines two styles:

- **Layers** separate code by technical responsibility and control which way dependencies point.
- **Vertical slices** separate code by feature so that each use case is self-contained.

```
┌──────────────────────────────────────────────────────────────┐
│  SpendScope.Web (Blazor WASM)   │  Browser                    │
└───────────────────────┬──────────────────────────────────────┘
                        │ HTTPS + JWT (JSON)
┌───────────────────────▼──────────────────────────────────────┐
│  SpendScope.Api  (composition root, endpoints discovery)      │
├──────────────────────────────────────────────────────────────┤
│  SpendScope.Features  (vertical slices)                       │
│    Expenses/ │ Categories/ │ Reports/ │ Admin/ │ Auth/        │
├──────────────────────────────────────────────────────────────┤
│  SpendScope.Application  (abstractions, pipeline behaviors)   │
├──────────────────────────────────────────────────────────────┤
│  SpendScope.Domain  (entities, value objects, rules)          │
├──────────────────────────────────────────────────────────────┤
│  SpendScope.Infrastructure  (EF Core, Identity, clock, email) │
└──────────────────────────────────────────────────────────────┘
```

### 10.2 Layer responsibilities

| Layer | Responsibilities | Must NOT |
|---|---|---|
| **Domain** | Entities, value objects, invariants, domain exceptions, `IClock` abstraction. | Reference any other SpendScope project or any framework (no EF Core, no ASP.NET). |
| **Application** | `IAppDbContext`, `ICurrentUser`, `IEmailSender`, `ITokenService` abstractions, `Result<T>`, validation and logging behaviors. | Reference Infrastructure or Features. |
| **Features** | Slices. Each slice has request, validator, handler, response, and endpoint. | Reference Infrastructure. Use `DbContext` directly. Reference another slice. |
| **Infrastructure** | `AppDbContext`, Identity configuration, EF migrations, token service implementation, email sender, clock implementation. | Contain business rules. |
| **Api** | `Program.cs`, DI wiring, middleware, endpoint discovery, rate limiting, auth configuration. | Contain business logic. |
| **Contracts** | Shared DTOs for requests and responses used by both API and Blazor client. | Contain any logic or references. |
| **Web** | Blazor WASM app: pages, components, typed API clients, auth state provider, chart interop. | Reference Domain, Infrastructure, or Features. |

### 10.3 Dependency rules

```
Web ──────────────► Contracts
Api ──► Features ──► Application ──► Domain
 │         │              ▲
 │         └──────────────┘
 └──► Infrastructure ──► Application
                          Contracts ◄── Features (slices return contract types)
```

Rules, each enforced by an architecture test:

| Rule ID | Rule |
|---|---|
| ARCH-01 | Domain depends on no SpendScope project and no framework namespace except `System.*`. |
| ARCH-02 | Application depends only on Domain. |
| ARCH-03 | Features depends only on Application, Domain, and Contracts. |
| ARCH-04 | Features slices do not reference other slices. Cross-slice sharing goes through Application. |
| ARCH-05 | Infrastructure does not reference Features. |
| ARCH-06 | Web does not reference Domain, Application, Features, or Infrastructure. |
| ARCH-07 | Every class named `*Handler` lives inside a `Features` namespace. |
| ARCH-08 | Every class implementing `IFeatureEndpoint` is public, non-abstract, and lives in Features. |
| ARCH-09 | Only Api and Infrastructure may reference `Microsoft.EntityFrameworkCore` types outside of Features query code. *(Features may use `IQueryable` extensions from EF Core only through `IAppDbContext`.)* |

### 10.4 Vertical slice anatomy

Each slice contains five files at most:

| File | Purpose | Example name |
|---|---|---|
| `*Request.cs` | Input record or query parameters | `CreateExpenseRequest.cs` |
| `*Validator.cs` | FluentValidation rules | `CreateExpenseValidator.cs` |
| `*Handler.cs` | Business logic, calls `IAppDbContext` | `CreateExpenseHandler.cs` |
| `*Response.cs` | Output contract (from `Contracts` project) | Uses `ExpenseDto` from Contracts |
| `*Endpoint.cs` | Implements `IFeatureEndpoint`, maps the route | `CreateExpenseEndpoint.cs` |

### 10.5 Request pipeline

For each request:

1. Middleware: correlation ID, security headers, exception handler, rate limiter (auth routes), authentication.
2. Endpoint: binds request, runs authorization policy.
3. Handler is invoked through a simple dispatcher (no MediatR in v1, see section 31 for the reasoning).
4. Validator runs before the handler. Failures return `400` with `ValidationProblemDetails`.
5. Handler returns `Result<T>`. The endpoint maps `Result` to HTTP status codes.

### 10.6 Result type

```csharp
public readonly record struct Result<T>(T? Value, Error? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(Error error) => new(default, error);
}

public sealed record Error(string Code, string Message, ErrorType Type);

public enum ErrorType { Validation, NotFound, Conflict, Unauthorized, Forbidden, Failure }
```

Mapping:

| `ErrorType` | HTTP status |
|---|---|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |
| Failure | 500 |

---

## 11. Solution structure

```
SpendScope/
├── SpendScope.sln
├── .editorconfig
├── Directory.Build.props              # shared settings: nullable, implicit usings, analyzers
├── Directory.Packages.props           # central package management
├── docker-compose.yml
├── README.md
├── docs/
│   ├── SpendScope_Project_Spec.md     # this document
│   ├── adr/                           # architecture decision records
│   └── api/                           # exported OpenAPI json
├── src/
│   ├── SpendScope.Domain/
│   │   ├── Common/
│   │   │   ├── Entity.cs
│   │   │   ├── DomainException.cs
│   │   │   └── IClock.cs
│   │   ├── Expenses/
│   │   │   ├── Expense.cs
│   │   │   └── ExpenseRules.cs
│   │   ├── Categories/
│   │   │   ├── Category.cs
│   │   │   └── CategoryRules.cs
│   │   ├── Users/
│   │   │   └── ApplicationUser.cs
│   │   ├── Auth/
│   │   │   └── RefreshToken.cs
│   │   └── ValueObjects/
│   │       ├── Money.cs
│   │       ├── EmailAddress.cs
│   │       └── HexColor.cs
│   ├── SpendScope.Application/
│   │   ├── Abstractions/
│   │   │   ├── IAppDbContext.cs
│   │   │   ├── ICurrentUser.cs
│   │   │   ├── ITokenService.cs
│   │   │   └── IEmailSender.cs
│   │   ├── Common/
│   │   │   ├── Result.cs
│   │   │   ├── Error.cs
│   │   │   ├── PagedResult.cs
│   │   │   └── Pagination.cs
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs
│   │   │   └── LoggingBehavior.cs
│   │   └── DependencyInjection.cs
│   ├── SpendScope.Contracts/
│   │   ├── Auth/
│   │   ├── Expenses/
│   │   ├── Categories/
│   │   ├── Reports/
│   │   └── Admin/
│   ├── SpendScope.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/        # IEntityTypeConfiguration per entity
│   │   │   ├── Migrations/
│   │   │   └── Seeding/
│   │   │       ├── IdentitySeeder.cs
│   │   │       └── CategorySeeder.cs
│   │   ├── Identity/
│   │   │   ├── IdentityConfiguration.cs
│   │   │   └── TokenService.cs
│   │   ├── Services/
│   │   │   ├── SystemClock.cs
│   │   │   └── ConsoleEmailSender.cs  # dev; SMTP in prod
│   │   └── DependencyInjection.cs
│   ├── SpendScope.Features/
│   │   ├── Auth/
│   │   │   ├── Register/
│   │   │   ├── Login/
│   │   │   ├── Refresh/
│   │   │   ├── Logout/
│   │   │   ├── ChangePassword/
│   │   │   ├── ForgotPassword/
│   │   │   └── GetProfile/
│   │   ├── Expenses/
│   │   │   ├── CreateExpense/
│   │   │   ├── UpdateExpense/
│   │   │   ├── DeleteExpense/
│   │   │   ├── GetExpense/
│   │   │   ├── ListExpenses/
│   │   │   └── ExportExpensesCsv/
│   │   ├── Categories/
│   │   │   ├── ListCategories/
│   │   │   ├── CreateCategory/
│   │   │   ├── UpdateCategory/
│   │   │   └── DeleteCategory/
│   │   ├── Reports/
│   │   │   ├── GetCategorySummary/
│   │   │   └── GetMonthlyTotals/
│   │   ├── Admin/
│   │   │   ├── ListUsers/
│   │   │   ├── SetUserLockout/
│   │   │   └── GetSystemTotals/
│   │   └── DependencyInjection.cs
│   ├── SpendScope.Api/
│   │   ├── Program.cs
│   │   ├── Endpoints/
│   │   │   ├── IFeatureEndpoint.cs
│   │   │   └── EndpointExtensions.cs
│   │   ├── Middleware/
│   │   │   ├── CorrelationIdMiddleware.cs
│   │   │   ├── SecurityHeadersMiddleware.cs
│   │   │   └── GlobalExceptionHandler.cs
│   │   ├── Auth/
│   │   │   └── JwtOptions.cs
│   │   ├── RateLimiting/
│   │   │   └── RateLimitPolicies.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── wwwroot/                   # hosts the Blazor WASM build output
│   └── SpendScope.Web/                # Blazor WebAssembly client
│       ├── Program.cs
│       ├── App.razor
│       ├── _Imports.razor
│       ├── Layout/
│       │   ├── MainLayout.razor
│       │   ├── NavMenu.razor
│       │   └── AuthLayout.razor
│       ├── Pages/
│       │   ├── Auth/
│       │   │   ├── Login.razor
│       │   │   ├── Register.razor
│       │   │   └── Profile.razor
│       │   ├── Dashboard.razor
│       │   ├── Expenses/
│       │   │   ├── ExpenseList.razor
│       │   │   └── ExpenseEditor.razor
│       │   ├── Categories/
│       │   │   └── CategoryManager.razor
│       │   ├── Admin/
│       │   │   ├── UserList.razor
│       │   │   └── SystemTotals.razor
│       │   ├── NotFound.razor
│       │   └── Error.razor
│       ├── Components/
│       │   ├── Charts/
│       │   │   ├── DoughnutChart.razor
│       │   │   └── BarChart.razor
│       │   ├── DateRangePicker.razor
│       │   ├── SummaryCard.razor
│       │   ├── CategoryBadge.razor
│       │   ├── ConfirmDialog.razor
│       │   ├── EmptyState.razor
│       │   └── ValidatedInput.razor
│       ├── Services/
│       │   ├── Auth/
│       │   │   ├── JwtAuthStateProvider.cs
│       │   │   ├── TokenStore.cs
│       │   │   └── AuthApiClient.cs
│       │   ├── Api/
│       │   │   ├── ExpenseApiClient.cs
│       │   │   ├── CategoryApiClient.cs
│       │   │   ├── ReportApiClient.cs
│       │   │   └── AdminApiClient.cs
│       │   ├── Http/
│       │   │   ├── AuthorizationHeaderHandler.cs
│       │   │   └── RefreshOnUnauthorizedHandler.cs
│       │   └── Charts/
│       │       └── ChartInterop.cs
│       ├── wwwroot/
│       │   ├── index.html
│       │   ├── css/app.css
│       │   └── js/charts.js
│       └── appsettings.json
└── tests/
    ├── SpendScope.UnitTests/
    ├── SpendScope.IntegrationTests/
    ├── SpendScope.ArchitectureTests/
    └── SpendScope.Web.Tests/          # bUnit component tests
```

### 11.1 Project references

| Project | References |
|---|---|
| Domain | none |
| Application | Domain |
| Contracts | none |
| Infrastructure | Application, Domain |
| Features | Application, Domain, Contracts |
| Api | Features, Infrastructure, Application, Contracts |
| Web | Contracts |
| UnitTests | Application, Domain, Features |
| IntegrationTests | Api |
| ArchitectureTests | Domain, Application, Features, Infrastructure, Api, Web |
| Web.Tests | Web |

### 11.2 Shared build settings

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

`Web` project overrides `TargetFramework` only if the SDK requires it. Blazor WASM targets `net10.0` as well.

---

## 12. Technology stack

| Concern | Choice | Version | Reason |
|---|---|---|---|
| Runtime | .NET | 10 LTS | Long-term support. |
| Web framework | ASP.NET Core Minimal APIs | Included | Lightweight endpoints. |
| ORM | EF Core | 10.x | Migrations, LINQ, query filters. |
| Database | PostgreSQL | 16 | Reliable `numeric` and `date` types. |
| DB provider | Npgsql.EntityFrameworkCore.PostgreSQL | 10.x | Official provider. |
| Identity | ASP.NET Core Identity | Included | Password hashing, lockout, roles. |
| JWT | Microsoft.AspNetCore.Authentication.JwtBearer | 10.x | Standard bearer validation. |
| Validation | FluentValidation | 11.x | Readable rules, reusable in tests. |
| Logging | Serilog.AspNetCore | 8.x | Structured logs, sinks. |
| Tracing | OpenTelemetry.Extensions.Hosting | 1.x | Traces and metrics. |
| Mapping | Manual mapping | N/A | Explicit and fast. Avoids AutoMapper licensing and magic. |
| Frontend | Blazor WebAssembly | 10.x | C# end to end, shared Contracts. |
| UI kit | MudBlazor | 8.x | Layouts, tables, dialogs, forms. |
| Charts | Chart.js | 4.4.1 | Mature, loaded via JS interop. |
| Component tests | bUnit | 1.x | Blazor component testing. |
| API tests | xUnit, WebApplicationFactory | 2.x | Standard. |
| Container tests | Testcontainers.PostgreSql | 4.x | Real PostgreSQL in tests. |
| Architecture tests | NetArchTest.Rules | 1.x | Dependency rule checks. |
| Mutation tests | Stryker.NET | 4.x | Optional, for core logic. |
| Containers | Docker, Docker Compose | Latest | Local and deployment parity. |
| CI | GitHub Actions | N/A | Build, test, image publish. |

> Pin exact versions in `Directory.Packages.props` with central package management. Use the latest patch releases available when you start.

---

## 13. Authentication and authorization design

### 13.1 Flow overview

```
Register ─► Identity creates user ─► assign role "User" ─► 201 Created

Login ─► validate password ─► check lockout
      ─► issue access token (JWT, 15 min)
      ─► issue refresh token (opaque, 14 days, stored hashed)
      ─► 200 { accessToken, refreshToken, expiresIn }

API call ─► Authorization: Bearer <access token>
         ─► JWT validated (signature, issuer, audience, lifetime)
         ─► claims → ClaimsPrincipal → policy check → handler

Refresh ─► hash incoming refresh token
        ─► find row; if revoked → revoke all user's tokens (reuse detected)
        ─► if expired → 401
        ─► revoke old row, create new row linked via ReplacedByTokenHash
        ─► issue new access token + new refresh token
```

### 13.2 Token format

**Access token (JWT) claims:**

| Claim | Source |
|---|---|
| `sub` | User ID (Guid as string) |
| `ClaimTypes.NameIdentifier` | Same as `sub` (mapped by the middleware) |
| `email` | Email |
| `name` | DisplayName |
| `role` | One claim per role |
| `jti` | Random Guid per token |
| `iss` | `SpendScope` |
| `aud` | `SpendScope.Clients` |
| `iat`, `nbf`, `exp` | Standard timestamps. Lifetime 15 minutes. |

**Signing:** HMAC-SHA256 with a 256-bit key from configuration. Minimum key length 32 characters, enforced at startup.

**Refresh token:** 64 random bytes, Base64URL encoded. Only the SHA-256 hash is persisted.

### 13.3 Password policy

| Rule | Value |
|---|---|
| Minimum length | 10 |
| Require digit | Yes |
| Require lowercase | Yes |
| Require uppercase | Yes |
| Require non-alphanumeric | Yes |
| Unique characters | 4 |

Configured via `IdentityOptions.Password`.

### 13.4 Lockout

| Setting | Value |
|---|---|
| `DefaultLockoutTimeSpan` | 15 minutes |
| `MaxFailedAccessAttempts` | 5 |
| `AllowedForNewUsers` | true |

Admins can lock accounts manually with a far-future `LockoutEnd`.

### 13.5 Refresh token rotation and reuse detection

When a refresh token that was already rotated is presented again, it signals theft. The server:

1. Revokes every active refresh token for that user.
2. Returns `401` with the code `refresh_token_reused`.
3. Writes a security log entry at `Warning` level with the user ID and IP address.

The user must log in again.

### 13.6 Logout

Logout revokes the presented refresh token. The access token remains valid until expiry (up to 15 minutes). This is a known trade-off. Section 21 explains how to mitigate it with a short lifetime and an optional denylist for `jti` values.

### 13.7 Blazor client token handling

| Token | Storage | Reason |
|---|---|---|
| Access token | In memory (a singleton `TokenStore`) | Not readable by other scripts after a page reload. Lost on reload, which is acceptable because the refresh token restores it. |
| Refresh token | `sessionStorage` in v1 | Survives reloads within a tab. Clears when the tab closes. |

> **Improved approach (planned for v1.1):** Store the refresh token in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie set by the API. This removes it from JavaScript reach entirely. Section 30 covers it.

Because the Blazor WASM app and the API share the same origin in production (hosted model), the cookie approach works without CORS complications.

### 13.8 Policies in code

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireUser", p => p.RequireAuthenticatedUser().RequireRole("User", "Admin"))
    .AddPolicy("RequireAdmin", p => p.RequireAuthenticatedUser().RequireRole("Admin"))
    .AddPolicy("ExpenseOwner", p => p.AddRequirements(new OwnerRequirement()));
```

Use `RequireUser` on feature endpoints. Use `RequireAdmin` on admin endpoints. Use `ExpenseOwner` through `IAuthorizationService` for single-resource operations.

### 13.9 Resource-based authorization

Single-resource endpoints (`GET`, `PUT`, `DELETE` by ID) do two things:

1. Load the expense with a query that already filters by `UserId` (defense layer 1).
2. If not found, return 404 (never 403).

The authorization handler is a second check that runs when the handler returns the entity, and it protects against future code paths that bypass the filter. A dedicated test verifies that bypass paths fail.

---

## 14. API design

### 14.1 Conventions

| Topic | Convention |
|---|---|
| Base path | `/api` |
| Format | JSON, `camelCase` property names |
| IDs | GUID in the route, lowercase, hyphenated |
| Dates | ISO 8601 date (`2026-03-15`) for `DateOnly`; UTC timestamps (`2026-03-15T10:30:00Z`) for times |
| Money | JSON number with two decimal places. Clients format for display. |
| Paging | `page` (1-based, default 1), `pageSize` (default 20, max 100) |
| Sorting | `sortBy` and `sortDir` (`asc` or `desc`), with a whitelist per endpoint |
| Errors | RFC 9457 `ProblemDetails`, plus an `errorCode` extension |
| Versioning | URL-free in v1. If breaking changes arrive, add `/api/v2`. |
| Correlation | Request header `X-Correlation-Id` is accepted or generated, and echoed in the response. |

### 14.2 Endpoint catalogue

**Auth**

| Method | Route | Auth | Purpose | Success | Notable errors |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Anonymous | Create account | 201 | 400, 409 |
| POST | `/api/auth/login` | Anonymous | Get tokens | 200 | 401, 423 (locked) |
| POST | `/api/auth/refresh` | Anonymous | Rotate tokens | 200 | 401 |
| POST | `/api/auth/logout` | User | Revoke refresh token | 204 | 401 |
| POST | `/api/auth/change-password` | User | Change password | 204 | 400, 401 |
| POST | `/api/auth/forgot-password` | Anonymous | Send reset email | 202 (always) | 400 |
| POST | `/api/auth/reset-password` | Anonymous | Apply reset | 204 | 400 |
| GET | `/api/me` | User | Current profile | 200 | 401 |
| PUT | `/api/me` | User | Update display name and currency | 200 | 400 |

**Expenses**

| Method | Route | Auth | Purpose | Success |
|---|---|---|---|---|
| GET | `/api/expenses` | User | List with filters, paging, sorting | 200 |
| POST | `/api/expenses` | User | Create | 201 + `Location` header |
| GET | `/api/expenses/{id}` | User | Get one | 200 |
| PUT | `/api/expenses/{id}` | User | Replace fields | 200 |
| DELETE | `/api/expenses/{id}` | User | Delete | 204 |
| GET | `/api/expenses/export` | User | CSV download, same filters as list | 200 (text/csv) |

**Categories**

| Method | Route | Auth | Purpose | Success |
|---|---|---|---|---|
| GET | `/api/categories` | User | System plus own categories | 200 |
| POST | `/api/categories` | User | Create personal category | 201 |
| PUT | `/api/categories/{id}` | User | Rename or recolor own | 200 |
| DELETE | `/api/categories/{id}?replacementId=` | User | Delete own, reassign expenses | 204 |

**Reports**

| Method | Route | Auth | Purpose | Success |
|---|---|---|---|---|
| GET | `/api/reports/summary?from&to` | User | Totals, counts, average, per-category breakdown | 200 |
| GET | `/api/reports/monthly?year` | User | Twelve monthly totals | 200 |

**Admin**

| Method | Route | Auth | Purpose | Success |
|---|---|---|---|---|
| GET | `/api/admin/users?page&pageSize&search` | Admin | List users | 200 |
| POST | `/api/admin/users/{id}/lock` | Admin | Lock account | 204 |
| POST | `/api/admin/users/{id}/unlock` | Admin | Unlock account | 204 |
| GET | `/api/admin/reports/category-totals?from&to` | Admin | System-wide aggregate | 200 |
| GET | `/api/admin/reports/monthly?year` | Admin | System-wide monthly aggregate | 200 |

**Health**

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/health/live` | Anonymous | Process is alive |
| GET | `/health/ready` | Anonymous | Database reachable |

### 14.3 Request and response examples

**Register**

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "priya@example.com",
  "password": "Str0ng!Passphrase",
  "displayName": "Priya Sharma"
}
```

```http
201 Created
Location: /api/me

{
  "id": "0196a3f2-7c1e-7d1a-9b2e-4f5a6b7c8d9e",
  "email": "priya@example.com",
  "displayName": "Priya Sharma"
}
```

**Login**

```http
POST /api/auth/login
Content-Type: application/json

{ "email": "priya@example.com", "password": "Str0ng!Passphrase" }
```

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "q3Xk...Base64Url",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```

**Create expense**

```http
POST /api/expenses
Authorization: Bearer eyJhbGciOi...
Content-Type: application/json

{
  "amount": 42.50,
  "occurredOn": "2026-03-14",
  "categoryId": "0196a3f3-1111-7d1a-9b2e-000000000001",
  "note": "Lunch with team"
}
```

```http
201 Created
Location: /api/expenses/0196a3f4-2222-7d1a-9b2e-000000000002

{
  "id": "0196a3f4-2222-7d1a-9b2e-000000000002",
  "amount": 42.50,
  "currency": "USD",
  "occurredOn": "2026-03-14",
  "category": { "id": "0196a3f3-1111-7d1a-9b2e-000000000001", "name": "Food and Dining", "colorHex": "#F59E0B" },
  "note": "Lunch with team",
  "createdAtUtc": "2026-03-14T12:01:33Z"
}
```

**List expenses (filtered and paged)**

```http
GET /api/expenses?from=2026-03-01&to=2026-03-31&categoryId=...&minAmount=10&sortBy=amount&sortDir=desc&page=1&pageSize=20
```

```json
{
  "items": [ { "id": "...", "amount": 42.50, "...": "..." } ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 37,
  "totalPages": 2
}
```

**Validation error**

```http
400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://spendscope.dev/errors/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errorCode": "validation_failed",
  "traceId": "00-4bf92f...-01",
  "correlationId": "c1d2e3f4",
  "errors": {
    "amount": ["Amount must be greater than 0."],
    "occurredOn": ["Date cannot be more than 1 year in the future."]
  }
}
```

**Not found (other user's expense)**

```http
404 Not Found
Content-Type: application/problem+json

{
  "type": "https://spendscope.dev/errors/not-found",
  "title": "Expense not found.",
  "status": 404,
  "errorCode": "expense_not_found"
}
```

### 14.4 Query parameter rules

| Parameter | Type | Default | Validation |
|---|---|---|---|
| `from`, `to` | date | `from` = first of current month, `to` = today | `from` ≤ `to`; range at most 366 days for expense lists |
| `categoryId` | guid | none | Must exist and be visible to the user |
| `minAmount`, `maxAmount` | decimal | none | ≥ 0; `minAmount` ≤ `maxAmount` |
| `page` | int | 1 | ≥ 1 |
| `pageSize` | int | 20 | 1 to 100 |
| `sortBy` | enum | `date` | `date`, `amount`, `category` |
| `sortDir` | enum | `desc` | `asc`, `desc` |
| `year` | int | current year | 2000 to current year |

### 14.5 Rate limits

| Policy | Scope | Limit |
|---|---|---|
| `auth-strict` | Login, register, forgot-password, reset-password | 5 requests per minute per IP |
| `auth-refresh` | Refresh | 30 requests per minute per IP |
| `api-default` | All authenticated endpoints | 300 requests per minute per user |

Exceeded limits return `429` with `Retry-After`.

### 14.6 Caching

| Endpoint | Cache | Notes |
|---|---|---|
| `GET /api/categories` | Response cache, 5 minutes, private | Invalidated on category change via version key. |
| Report endpoints | None in v1 | Results depend on fresh data. Revisit if slow. |

---

## 15. Feature slice specifications

Each slice lists: purpose, input, validation, handler logic, output, errors, and tests.

### 15.1 Register

- **Purpose:** Create a user account.
- **Input:** `RegisterRequest(string Email, string Password, string DisplayName)`
- **Validation:** Email format and length; password per section 13.3; display name 2 to 50 characters.
- **Logic:**
  1. Check for an existing user with the normalized email. If found, return `Conflict`.
  2. Create `ApplicationUser` with `UserName = Email`, `DefaultCurrency = "USD"`.
  3. Call `UserManager.CreateAsync` with the password.
  4. Add role `User`.
  5. Commit. Return `UserDto`.
- **Errors:** Conflict (409) if email exists. Validation (400) for policy violations. Identity error messages are mapped to the `password` key.
- **Tests:** Success path; duplicate email (case-insensitive); weak password; role assigned.

### 15.2 Login

- **Input:** `LoginRequest(string Email, string Password)`
- **Logic:**
  1. Find user by normalized email. If not found, perform a dummy password check (to equalize timing) and return `Unauthorized`.
  2. If locked out, return `Locked` (423) with `Retry-After` header.
  3. `CheckPasswordSignInAsync` with `lockoutOnFailure: true`.
  4. On success, issue access and refresh tokens.
- **Output:** `TokenResponse` (access token, refresh token, expires in seconds).
- **Tests:** Success; wrong password; unknown email returns same message as wrong password; lockout after 5 failures; lockout expiry.

### 15.3 Refresh

- **Input:** `RefreshRequest(string RefreshToken)`
- **Logic:** Implements section 13.5.
- **Tests:** Rotation produces a new token; old token rejected; reuse revokes all tokens; expired token rejected.

### 15.4 Logout

- **Input:** `LogoutRequest(string RefreshToken)`
- **Logic:** Hash token, mark revoked. If token not found, still return 204 (idempotent).
- **Tests:** Revoked token cannot refresh; second logout returns 204.

### 15.5 Change password

- **Input:** `ChangePasswordRequest(string CurrentPassword, string NewPassword)`
- **Logic:** `UserManager.ChangePasswordAsync`. On success, revoke all refresh tokens for the user.
- **Tests:** Wrong current password; success revokes refresh tokens.

### 15.6 Forgot and reset password

- **Forgot input:** `ForgotPasswordRequest(string Email)`
- **Logic:** If the user exists and is not locked, generate a reset token and send email. Always return 202.
- **Reset input:** `ResetPasswordRequest(string Email, string Token, string NewPassword)`
- **Tests:** Response identical for known and unknown emails (same status, same body, similar timing); expired token rejected; token single use.

### 15.7 Get and update profile

- **Get:** Returns `UserProfileDto(Id, Email, DisplayName, DefaultCurrency)`.
- **Update input:** `UpdateProfileRequest(string DisplayName, string DefaultCurrency)`. Currency must be in the allowed list (USD, EUR, GBP, INR, JPY, AUD, CAD).
- **Tests:** Update persists; invalid currency rejected.

### 15.8 Create expense

- **Input:** `CreateExpenseRequest(decimal Amount, DateOnly OccurredOn, Guid CategoryId, string? Note)`
- **Validation:** Amount rules (FR-EXP-02); date rule (FR-EXP-03); note max 500 characters.
- **Logic:**
  1. Load category where `Id == CategoryId` and (`IsSystem` or `UserId == current user`). If missing, return `Validation` error on `categoryId`.
  2. Load user default currency.
  3. `Expense.Create(userId, Money(amount, currency), occurredOn, categoryId, note, clock.UtcNow)`.
  4. Add and save.
- **Output:** `ExpenseDto`.
- **Tests:** Success; zero amount; future date beyond one year; another user's category rejected; note too long.

### 15.9 Update expense

- **Input:** `UpdateExpenseRequest(Guid Id, decimal Amount, DateOnly OccurredOn, Guid CategoryId, string? Note)`
- **Logic:** Load with user filter; if missing, 404. Apply same validation as create. Set `UpdatedAtUtc`. Currency is not editable (keeps history consistent).
- **Tests:** Success; other user's expense returns 404; category change applies; `UpdatedAtUtc` set.

### 15.10 Delete expense

- **Logic:** Load with user filter; if missing, 404. Remove and save.
- **Tests:** Success; other user's expense returns 404 and is not deleted; double delete returns 404.

### 15.11 Get expense

- **Logic:** Load with user filter, include category. If missing, 404.
- **Tests:** Success; other user's expense returns 404.

### 15.12 List expenses

- **Input:** `ListExpensesQuery` with filters from section 14.4.
- **Logic:**
  1. Start from `db.Expenses` (global filter already applies user scope).
  2. Apply filters.
  3. Count total before paging.
  4. Apply sort with whitelist and a stable secondary sort on `Id`.
  5. Page with `Skip` and `Take`. Project directly to `ExpenseDto` in SQL.
- **Tests:** Paging boundaries; filter combinations; sort stability; other user's rows never appear; empty result returns empty list with `totalCount: 0`.

### 15.13 Export expenses as CSV

- **Logic:** Same filter as list, no paging, capped at 50,000 rows (returns 422 above that). Stream rows with `StreamWriter`. Escape values that begin with `=`, `+`, `-`, or `@` to prevent CSV injection.
- **Headers:** `Content-Type: text/csv`, `Content-Disposition: attachment; filename="spendscope-2026-03.csv"`.
- **Tests:** Header row present; formula injection escaped; row count matches filter.

### 15.14 List categories

- **Logic:** Return system categories plus user categories ordered by `IsSystem` desc, then name.
- **Tests:** Other user's categories excluded; system categories included.

### 15.15 Create category

- **Input:** `CreateCategoryRequest(string Name, string ColorHex)`
- **Logic:** Normalize name. Check uniqueness against system names and user's own names. Return `Conflict` if taken.
- **Tests:** Duplicate of system name rejected; duplicate of own name rejected; case-insensitive match; invalid hex rejected.

### 15.16 Update category

- **Logic:** Load user-owned category. System category → `Forbidden` (403) with code `system_category_read_only`. Missing → 404.
- **Tests:** System category update returns 403; other user's category returns 404; rename to existing name returns 409.

### 15.17 Delete category

- **Input:** `replacementId` query parameter, required only if expenses use the category.
- **Logic:**
  1. Load user-owned category. Missing → 404. System → 403.
  2. Count expenses using it.
  3. If count > 0 and no `replacementId`, return `Validation` error `replacement_required`.
  4. If `replacementId` given, verify it is visible to user. Reassign expenses in one `ExecuteUpdateAsync`.
  5. Delete category. All inside one transaction.
- **Tests:** Delete with no expenses; delete with expenses requires replacement; reassignment moves all expenses; transaction rolls back on failure.

### 15.18 Summary report

Covered in section 16.

### 15.19 Monthly totals report

Covered in section 16.

### 15.20 Admin: list users

- **Input:** `page`, `pageSize`, `search` (matches email prefix, case-insensitive).
- **Output:** `AdminUserDto(Id, Email, DisplayName, Roles, IsLocked, CreatedAtUtc)`. No expense data.
- **Tests:** Admin sees all users; non-admin gets 403; search filters correctly; response contains no expense fields.

### 15.21 Admin: lock and unlock

- **Logic:** Target user must exist (404). Cannot target self (400, code `cannot_lock_self`). Lock sets `LockoutEnd` to `DateTimeOffset.MaxValue`. Unlock sets it to null and resets failed count. Revokes all refresh tokens for a locked user.
- **Tests:** Lock blocks login; unlock restores login; self-lock rejected; non-admin 403.

### 15.22 Admin: system totals

- **Logic:** Aggregate across all expenses by category, for a date range. Returns only category totals and counts, never user IDs.
- **Tests:** Totals match seeded data; response has no `userId` field; non-admin 403.

---

## 16. Reports and aggregation

### 16.1 Summary report

**Endpoint:** `GET /api/reports/summary?from=&to=`

**Response:**

```json
{
  "from": "2026-03-01",
  "to": "2026-03-31",
  "currency": "USD",
  "grandTotal": 1842.75,
  "expenseCount": 46,
  "averageExpense": 40.06,
  "categories": [
    { "categoryId": "...", "name": "Groceries", "colorHex": "#84CC16", "total": 612.40, "count": 14, "percentage": 33.23 },
    { "categoryId": "...", "name": "Housing", "colorHex": "#8B5CF6", "total": 900.00, "count": 2, "percentage": 48.84 }
  ]
}
```

**Rules:**
- `percentage` = category total ÷ grand total × 100, rounded to two decimals. Sum may differ from 100 by ±0.01 due to rounding; document this in the UI footnote.
- If `grandTotal` is zero, `percentage` is 0 for all rows.
- Categories with zero spend are omitted.
- Currency is the user's default. All rows are in that currency.

**Query shape (EF Core):**

```csharp
var rows = await db.Expenses
    .Where(e => e.OccurredOn >= from && e.OccurredOn <= to)
    .GroupBy(e => new { e.CategoryId, e.Category.Name, e.Category.ColorHex })
    .Select(g => new
    {
        g.Key.CategoryId,
        g.Key.Name,
        g.Key.ColorHex,
        Total = g.Sum(e => e.Amount),
        Count = g.Count()
    })
    .OrderByDescending(x => x.Total)
    .ToListAsync(ct);
```

Compute grand total and percentages in C# from the grouped result (a small list), not from the raw rows.

### 16.2 Monthly totals report

**Endpoint:** `GET /api/reports/monthly?year=2026`

**Response:**

```json
{
  "year": 2026,
  "currency": "USD",
  "months": [
    { "month": 1, "total": 1500.00, "count": 38 },
    { "month": 2, "total": 0, "count": 0 },
    { "month": 3, "total": 1842.75, "count": 46 }
  ]
}
```

**Rules:**
- Always returns 12 entries, zero-filled for empty months.
- Grouping by `OccurredOn.Year` and `OccurredOn.Month` is done in SQL. In Npgsql, `EF.Functions.DateDiffMonth` or grouping on a computed `DateOnly` range works. Simplest reliable approach: group by `OccurredOn` month key using `e.OccurredOn.Month` (supported by Npgsql translation).

### 16.3 Date range rules

- Dashboard default: current month.
- Maximum range for summary: 366 days. Larger ranges return 400 with code `range_too_large`.
- Time zone: dates are calendar dates from the user's perspective. No conversion is performed server-side.

### 16.4 Performance targets and indexes

| Query | Expected index | Target (100k rows) |
|---|---|---|
| List, page 1 | `IX_Expenses_UserId_OccurredOn` | under 50 ms |
| Summary, 12 months | `IX_Expenses_UserId_CategoryId_OccurredOn` | under 200 ms |
| Monthly, 1 year | `IX_Expenses_UserId_OccurredOn` | under 150 ms |

Verify with `EXPLAIN ANALYZE` in an integration test log. Record results in `docs/perf/`.

### 16.5 Admin aggregates

Same shape as user reports, but without the user filter. The query must not return `UserId`. Add an integration test that asserts the JSON has no `userId` property.

---

## 17. Validation rules

Validation runs in the Application pipeline through FluentValidation. Client-side validation mirrors these rules for user feedback but the server is authoritative.

| Field | Rules | Error code |
|---|---|---|
| Email | Required, valid format, ≤ 256 chars | `email_invalid` |
| Password | Min 10, upper, lower, digit, non-alphanumeric, ≥ 4 unique chars | `password_policy` |
| DisplayName | Required, 2 to 50 chars, trimmed, no control characters | `display_name_length` |
| DefaultCurrency | Must be in allowed list | `currency_unsupported` |
| Amount | > 0, ≤ 1,000,000.00, ≤ 2 decimal places | `amount_range` / `amount_precision` |
| OccurredOn | ≤ today + 365 days | `date_future` |
| Note | ≤ 500 chars | `note_length` |
| Category Name | 1 to 40 chars, trimmed, no leading or trailing spaces | `category_name_length` |
| ColorHex | `^#[0-9A-Fa-f]{6}$` | `color_invalid` |
| Page | ≥ 1 | `page_invalid` |
| PageSize | 1 to 100 | `page_size_invalid` |

**Sanitization:** Trim all strings. Reject control characters. Output encoding is handled by Blazor (it escapes by default). Never use `MarkupString` with user input.

---

## 18. Error handling

### 18.1 Global exception handler

`GlobalExceptionHandler` implements `IExceptionHandler` (ASP.NET Core 8+).

| Exception | Status | errorCode |
|---|---|---|
| `DomainException` | 422 | `domain_rule_violation` |
| `DbUpdateConcurrencyException` | 409 | `concurrency_conflict` |
| `OperationCanceledException` (client disconnected) | No response written | N/A |
| Any other | 500 | `internal_error` (no stack trace in body) |

Stack traces are logged with the correlation ID and never returned in production responses.

### 18.2 Concurrency

- `Expense` and `Category` include a `RowVersion` (`xmin` in PostgreSQL, mapped with `.IsRowVersion()` or `.UseXminAsConcurrencyToken()`).
- `PUT` requests may include an `If-Match` header with the ETag. A mismatch returns 412.
- Without `If-Match`, last write wins. Document this as the v1 behavior.

### 18.3 Client error mapping (Blazor)

| Status | UI behavior |
|---|---|
| 400 | Show field errors next to inputs |
| 401 | Attempt refresh once, then redirect to login |
| 403 | Show "You don't have permission" banner |
| 404 | Show "Not found" empty state |
| 409 | Show "This was changed by someone else. Reload and try again." |
| 423 | Show "Account temporarily locked. Try again in N minutes." |
| 429 | Show "Too many requests. Wait a moment." |
| 5xx | Show generic error with correlation ID for support |
| Network failure | Show "Can't reach SpendScope. Check your connection." |

---

## 19. Blazor frontend design

### 19.1 Hosting model

The solution uses the **hosted Blazor WebAssembly** template. The `SpendScope.Api` project serves the compiled WASM files from `wwwroot`. This gives:
- Same origin for the app and API (no CORS for production).
- One deployable artifact.
- Access to `Contracts` as a shared project reference.

During development, run the API and the Web project separately with a proxy, or use the hosted template's default dev setup.

### 19.2 Application shell

```
┌────────────────────────────────────────────────────────────┐
│ ☰  SpendScope                          Priya S. ▾  Logout  │  AppBar
├───────────┬────────────────────────────────────────────────┤
│ Dashboard │                                                │
│ Expenses  │             Page content                       │
│ Categories│                                                │
│ Profile   │                                                │
│ ── Admin ─│                                                │
│ Users     │                                                │
│ Totals    │                                                │
└───────────┴────────────────────────────────────────────────┘
```

- MudBlazor `MudLayout` with `MudAppBar`, `MudDrawer`, `MudMainContent`.
- Admin section appears only when the user has the `Admin` role.
- Drawer collapses to icons on screens narrower than 960 px and becomes a temporary overlay under 600 px.

### 19.3 Routes

| Route | Page | Auth | Notes |
|---|---|---|---|
| `/login` | Login | Anonymous only | Redirects to `/` if already signed in. |
| `/register` | Register | Anonymous only | |
| `/` | Dashboard | User | Default landing page. |
| `/expenses` | ExpenseList | User | Filters, table, paging. |
| `/expenses/new` | ExpenseEditor | User | Create mode. |
| `/expenses/{id:guid}/edit` | ExpenseEditor | User | Edit mode. |
| `/categories` | CategoryManager | User | |
| `/profile` | Profile | User | Name, currency, password change. |
| `/admin/users` | UserList | Admin | |
| `/admin/totals` | SystemTotals | Admin | |
| `/forgot-password` | ForgotPassword | Anonymous | |
| `/reset-password` | ResetPassword | Anonymous | Reads email and token from query string. |
| `/not-found` | NotFound | Any | |

Protect pages with `@attribute [Authorize]` or `@attribute [Authorize(Roles = "Admin")]`. Redirect unauthenticated users to `/login?returnUrl=...`.

### 19.4 Authentication state in Blazor WASM

**`JwtAuthStateProvider : AuthenticationStateProvider`**

- Holds the current `ClaimsPrincipal`.
- On login, parses the JWT (without validating signature, since the server signs it and the client only reads claims) and builds claims.
- Raises `NotifyAuthenticationStateChanged` after login, logout, and refresh.
- Initializes on app start by reading the refresh token from `sessionStorage` and calling `/api/auth/refresh`.

**`TokenStore`** (singleton)
- `string? AccessToken`, `DateTimeOffset AccessTokenExpiresAt`, `string? RefreshToken`.
- Refresh token is written to `sessionStorage` through a JS interop helper.

**`AuthorizationHeaderHandler : DelegatingHandler`**
- Adds `Authorization: Bearer <token>` if the access token exists and expires in more than 30 seconds.
- If it expires within 30 seconds, calls refresh first (single-flight: concurrent calls share one refresh task).

**`RefreshOnUnauthorizedHandler : DelegatingHandler`**
- On 401, attempts one refresh and retries the original request once. If the retry also fails, signs the user out.

### 19.5 Typed API clients

Each client wraps `HttpClient` and returns `Result<T>` (the same shape as the backend, or a lightweight client-side equivalent) so pages can handle errors without try-catch noise.

```csharp
public sealed class ExpenseApiClient(HttpClient http)
{
    public async Task<ApiResult<PagedResponse<ExpenseDto>>> ListAsync(
        ExpenseQuery query, CancellationToken ct = default)
    {
        var url = $"api/expenses{query.ToQueryString()}";
        using var response = await http.GetAsync(url, ct);
        return await ApiResult<PagedResponse<ExpenseDto>>.FromResponseAsync(response, ct);
    }

    public Task<ApiResult<ExpenseDto>> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default)
        => PostAsync<CreateExpenseRequest, ExpenseDto>("api/expenses", request, ct);

    // Other methods follow the same pattern.
}
```

Register clients with `AddHttpClient<ExpenseApiClient>` and attach the two delegating handlers, in this order: `AuthorizationHeaderHandler`, then `RefreshOnUnauthorizedHandler`.

### 19.6 Pages in detail

#### Dashboard

**Layout (top to bottom):**
1. Date range picker: preset chips (This month, Last 3 months, This year, Custom) and two date fields.
2. Summary cards row: Total spend, Expense count, Average expense, Top category.
3. Two-column row on wide screens, stacked on narrow:
   - Left: Doughnut chart of spend by category, with legend that shows name, amount, and percentage.
   - Right: Category table (name, total, count, percentage).
4. Full-width monthly bar chart for the selected year, with a year selector.
5. Empty state when the range has no expenses: illustration placeholder, text "No expenses in this range", and a button "Add an expense".

**Behavior:**
- Fetches summary and monthly data in parallel with `Task.WhenAll`.
- Debounces date changes by 300 ms before fetching.
- Shows a skeleton loader while loading.
- Preserves the selected range in the URL query string (`?from=...&to=...`) so a refresh keeps state.

#### Expense list

- MudTable with server-side paging, sorting, and filters.
- Columns: Date, Category (colored badge), Amount (right-aligned, formatted with the user's currency), Note (truncated, full text on hover), Actions (edit, delete).
- Filter bar: date range, category multi-select, min and max amount, search by note text (client-side on the current page only in v1; server search is future work).
- "Export CSV" button uses the current filters.
- Delete uses `ConfirmDialog`. Success shows a snackbar with "Undo" for 5 seconds. Undo re-creates the expense. (Implementation note: undo re-posts the original payload; document the limitation that the ID changes.)

#### Expense editor

- Fields: Amount (numeric, two decimals), Date (date picker, default today), Category (select), Note (multiline, counter at 500).
- Keyboard: Enter submits. Escape cancels with confirmation if dirty.
- Save button disabled while submitting. Shows server validation errors next to fields.
- On success, returns to the list and shows a snackbar.

#### Category manager

- List with color swatch, name, and expense count.
- System categories shown with a lock icon and disabled actions.
- Add dialog: name and a color picker (MudColorPicker or a palette of 12 preset colors plus a hex text field).
- Delete dialog: if the category has expenses, requires choosing a replacement category from a dropdown.

#### Profile

- Display name and default currency (saves with PUT /api/me).
- Change password section: current, new, confirm. Client checks that new and confirm match before sending.
- Logout button (revokes refresh token).

#### Login and register

- Login: email, password, "Remember this browser" checkbox (controls whether refresh token persists in `sessionStorage` beyond the tab; in v1 both behave the same, so the checkbox is hidden until cookie refresh is added).
- Register: email, display name, password with a strength meter and the policy checklist shown live.
- Both pages show generic error messages for failed login (no hint about whether the email exists).

#### Admin users

- MudTable with server paging, search box (email prefix).
- Columns: Email, Display name, Roles (chips), Status (Active or Locked), Created, Actions.
- Actions: Lock or Unlock. Lock requires confirmation. The button is disabled for the signed-in admin's own row.

#### Admin totals

- Date range and year selectors (same components as dashboard).
- Doughnut and bar charts of system-wide totals.
- A note at the top: "Aggregated across all users. Individual expenses are not shown."

### 19.7 Charts via JS interop

**`wwwroot/js/charts.js`**

```javascript
window.spendscopeCharts = {
  instances: {},

  render(canvasId, config) {
    this.destroy(canvasId);
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    this.instances[canvasId] = new Chart(ctx, config);
  },

  destroy(canvasId) {
    const existing = this.instances[canvasId];
    if (existing) {
      existing.destroy();
      delete this.instances[canvasId];
    }
  }
};
```

**`ChartInterop.cs`** wraps `IJSRuntime` and exposes `RenderAsync(string canvasId, object config)` and `DisposeAsync`. Config objects are built in C# and serialized to Chart.js config JSON.

**`DoughnutChart.razor`** (outline)

```razor
@implements IAsyncDisposable
@inject ChartInterop Charts

<canvas id="@CanvasId" role="img" aria-label="@AriaLabel"></canvas>

@code {
    [Parameter, EditorRequired] public IReadOnlyList<CategoryTotal> Data { get; set; } = [];
    [Parameter] public string CanvasId { get; set; } = $"chart-{Guid.NewGuid():N}";
    [Parameter] public string AriaLabel { get; set; } = "Spending by category";

    protected override async Task OnParametersSetAsync()
        => await Charts.RenderDoughnutAsync(CanvasId, Data);

    public async ValueTask DisposeAsync()
        => await Charts.DestroyAsync(CanvasId);
}
```

**Accessibility for charts:** Every chart is followed by a text table (or the category table on the dashboard) with the same numbers. The `aria-label` summarizes the chart. Colors come from category color hex values and always pair with labels in the legend.

**Chart.js setup:** Load Chart.js from jsDelivr with an exact version in `index.html`:

```html
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<script src="js/charts.js"></script>
```

### 19.8 Forms and validation in Blazor

- Use `EditForm` with a `DataAnnotationsValidator` for simple fields, or a FluentValidation-based validator for shared rules.
- Show server errors by mapping `errors` from ProblemDetails to `ValidationMessageStore`.
- Disable submit while a request is in flight. Prevents double-submit.

### 19.9 Formatting

| Value | Format |
|---|---|
| Money | `ToString("C", CultureInfo.CurrentCulture)` with the user's currency code. Provide a helper `Money.Format(amount, currency)`. |
| Date | `ToString("d MMM yyyy")`, for example `14 Mar 2026`. |
| Percentage | `ToString("0.0")` followed by `%`. |
| Large numbers | Compact notation for chart axes only (`1.2k`). |

Set the WASM culture from the browser; allow override in the profile later.

### 19.10 Styling

- MudBlazor theme with a custom palette: primary `#0F766E` (teal), secondary `#F59E0B` (amber), background `#F8FAFC`.
- Dark mode toggle in the app bar, persisted in `localStorage` (only the theme preference, not auth data).
- Typography: MudBlazor default (Roboto), with a system font fallback.
- Spacing scale: 4, 8, 12, 16, 24, 32 px.
- Breakpoints follow MudBlazor (`xs`, `sm`, `md`, `lg`, `xl`).

### 19.11 Performance in the browser

- Enable AOT compilation for production builds (`<RunAOTCompilation>true</RunAOTCompilation>`) and measure download size. Target under 4 MB compressed.
- Lazy load admin pages with a separate assembly if size becomes an issue.
- Use `@key` on list rows.
- Virtualize the category table if it exceeds 100 rows (MudBlazor `Virtualize`).

### 19.12 Frontend tests (bUnit)

| Component | Tests |
|---|---|
| DoughnutChart | Calls `RenderDoughnutAsync` with correct labels and values; disposes on removal. |
| ExpenseEditor | Shows validation errors from API; disables submit while saving; navigates on success. |
| DateRangePicker | Preset chips set correct dates; custom range rejects `from > to`. |
| SummaryCard | Formats money with currency; handles zero. |
| ConfirmDialog | Returns true on confirm, false on cancel. |
| Login | Shows generic error on 401; shows lockout message on 423. |

---

## 20. Logging and observability

### 20.1 Structured logging

- Serilog with console sink in development and JSON console sink in production.
- Enrichers: `CorrelationId`, `UserId` (when authenticated), `MachineName`, `Environment`.
- Log levels: `Information` for requests and business events; `Warning` for auth failures and lockouts; `Error` for unhandled exceptions.
- Never log passwords, tokens, or full expense notes. Notes are logged only as length.

### 20.2 Request logging

Serilog request logging middleware, with these fields: method, path (without query string), status code, elapsed ms, user ID, correlation ID. Query strings are excluded because they can include filters with personal data.

### 20.3 Traces and metrics (OpenTelemetry)

**Traces:** ASP.NET Core, HttpClient, EF Core, Npgsql instrumentations.

**Custom metrics:**

| Metric | Type | Tags |
|---|---|---|
| `spendscope.expenses.created` | Counter | none |
| `spendscope.logins.failed` | Counter | reason |
| `spendscope.refresh.reuse_detected` | Counter | none |
| `spendscope.report.duration` | Histogram (ms) | report_type |

**Export:** OTLP to a local OpenTelemetry Collector in Docker Compose, forwarded to Jaeger for traces and Prometheus for metrics. Grafana dashboard JSON stored in `docs/observability/`.

### 20.4 Health checks

- `live`: always healthy if the process responds.
- `ready`: checks PostgreSQL connectivity with `AddNpgSql`.

### 20.5 Correlation IDs

`CorrelationIdMiddleware`:
1. Read `X-Correlation-Id` from request. If missing or longer than 64 characters or contains invalid characters, generate a new one.
2. Store in `HttpContext.Items` and in Serilog's `LogContext`.
3. Echo in response header.
4. Include in every ProblemDetails body.

---

## 21. Security checklist

| # | Control | Implementation | Verified by |
|---|---|---|---|
| S-01 | Password hashing | Identity default (PBKDF2, HMAC-SHA256, high iteration count) | Unit test checks `PasswordHash` is not equal to plaintext |
| S-02 | Account lockout | Section 13.4 | Integration test |
| S-03 | Timing-safe unknown-user login | Dummy hash check on unknown email | Timing test (loose, documented) |
| S-04 | Enumeration resistance | Same response for forgot-password and register-conflict messages where feasible | Integration test |
| S-05 | Refresh token hashing | SHA-256 stored, plain never stored | Unit test |
| S-06 | Refresh token rotation and reuse detection | Section 13.5 | Integration test |
| S-07 | JWT validation | Issuer, audience, lifetime, signing key, clock skew 30 seconds | Integration test with tampered token |
| S-08 | Signing key length | Startup check ≥ 32 characters | Startup test |
| S-09 | Owner isolation | Global query filter plus explicit `Where` plus resource authorization | Integration test matrix |
| S-10 | IDOR | GUIDs, 404 on not owned | Integration test |
| S-11 | Mass assignment | Explicit request DTOs, no entity binding | Code review, unit test |
| S-12 | CSV injection | Escape leading `= + - @` | Unit test |
| S-13 | XSS | Blazor escaping, no `MarkupString` with user data | Code review, bUnit test with `<script>` in note |
| S-14 | CSRF | Access token in `Authorization` header (not cookie) for API. Refresh cookie (v1.1) uses `SameSite=Strict` plus anti-forgery on refresh endpoint. | Review |
| S-15 | CORS | Not needed in hosted mode. If split deployment, allow only the Web origin. | Config review |
| S-16 | Security headers | Middleware sets CSP, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` | Integration test checks headers |
| S-17 | HTTPS and HSTS | Redirect and HSTS outside development | Config review |
| S-18 | Rate limiting | Section 14.5 | Integration test |
| S-19 | Secrets management | User secrets in dev, environment variables or vault in prod. Never in source. | Git secret scanning in CI |
| S-20 | Dependency scanning | `dotnet list package --vulnerable` in CI | CI step |
| S-21 | Logging hygiene | No secrets, tokens, or note contents in logs | Code review |
| S-22 | Error detail | No stack traces in responses | Integration test |
| S-23 | Admin separation | Admin endpoints return no expense rows | Integration test with JSON assertion |
| S-24 | Input length limits | Enforced in validators and DB column sizes | Unit tests |
| S-25 | Content Security Policy | `default-src 'self'; script-src 'self' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline'; img-src 'self' data:` | Header test |

### 21.1 Known limitations (documented)

- Access tokens remain valid for up to 15 minutes after logout. Mitigation: short lifetime. Optional: `jti` denylist in Redis (section 30).
- Refresh token in `sessionStorage` is readable by script if XSS occurs. Mitigation: CSP, Blazor escaping, and the cookie migration in v1.1.
- Unknown-user timing equalization is approximate.

---

## 22. Configuration

### 22.1 `appsettings.json` (API, non-secret)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=spendscope;Username=spendscope;Password=from-secrets"
  },
  "Jwt": {
    "Issuer": "SpendScope",
    "Audience": "SpendScope.Clients",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 14,
    "ClockSkewSeconds": 30
  },
  "Identity": {
    "LockoutMinutes": 15,
    "MaxFailedAttempts": 5
  },
  "Expenses": {
    "MaxAmount": 1000000.00,
    "MaxFutureDays": 365,
    "MaxExportRows": 50000
  },
  "RateLimiting": {
    "AuthStrictPerMinute": 5,
    "AuthRefreshPerMinute": 30,
    "ApiPerMinute": 300
  },
  "Email": {
    "Provider": "Console",
    "From": "no-reply@spendscope.local",
    "Smtp": { "Host": "", "Port": 587, "UseSsl": true }
  },
  "Cors": {
    "AllowedOrigins": []
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" }
  }
}
```

### 22.2 Secrets (user secrets in development)

```bash
dotnet user-secrets init --project src/SpendScope.Api
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project src/SpendScope.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=spendscope;Username=spendscope;Password=localdev" --project src/SpendScope.Api
dotnet user-secrets set "Seed:Admin:Email" "admin@spendscope.local" --project src/SpendScope.Api
dotnet user-secrets set "Seed:Admin:Password" "Adm1n!LocalOnly" --project src/SpendScope.Api
```

Production: environment variables with double underscore syntax, for example `Jwt__SigningKey`, injected by the container platform or a secrets manager.

### 22.3 Configuration validation

Each options class uses `ValidateDataAnnotations()` and `ValidateOnStart()`. The app refuses to start with an invalid configuration. Example: `SigningKey` must be at least 32 characters.

### 22.4 Feature flags (optional)

Add `Microsoft.FeatureManagement` if you want to toggle features like CSV export without redeploying. Not required for v1.

---

## 23. Testing strategy

### 23.1 Test pyramid

| Level | Project | Share | Tools | Focus |
|---|---|---|---|---|
| Architecture | ArchitectureTests | Rules only | NetArchTest.Rules | Dependency rules |
| Unit | UnitTests | ~50% | xUnit, FluentAssertions or Shouldly, NSubstitute | Domain invariants, handlers with fake DbContext, validators |
| Component | Web.Tests | ~15% | bUnit | Blazor components |
| Integration | IntegrationTests | ~30% | WebApplicationFactory, Testcontainers PostgreSQL | Endpoints, auth, isolation, reports |
| End to end | (optional) | ~5% | Playwright | Login, create expense, view chart |

### 23.2 Architecture tests

| Test | Rule |
|---|---|
| `Domain_has_no_dependency_on_other_projects` | ARCH-01 |
| `Application_depends_only_on_Domain` | ARCH-02 |
| `Features_does_not_reference_Infrastructure` | ARCH-03 |
| `Slices_do_not_reference_each_other` | ARCH-04 (checks namespaces under `Features.*`) |
| `Infrastructure_does_not_reference_Features` | ARCH-05 |
| `Web_does_not_reference_server_projects` | ARCH-06 |
| `Handlers_live_in_Features` | ARCH-07 |
| `Endpoints_are_public_and_in_Features` | ARCH-08 |
| `Only_Infrastructure_uses_DbContext_directly` | ARCH-09 |

### 23.3 Unit test catalogue (examples)

| ID | Target | Scenario | Expected |
|---|---|---|---|
| UT-DOM-01 | `Expense.Create` | Amount is zero | Throws `DomainException` |
| UT-DOM-02 | `Expense.Create` | Amount is 1,000,000.01 | Throws |
| UT-DOM-03 | `Expense.Create` | Date is today + 366 | Throws |
| UT-DOM-04 | `Expense.Create` | Date is today + 365 | Succeeds |
| UT-DOM-05 | `Money` | Add different currencies | Throws |
| UT-VAL-01 | `CreateExpenseValidator` | Note of 501 chars | Error on `note` |
| UT-VAL-02 | `CreateExpenseValidator` | Amount with 3 decimals | Error on `amount` |
| UT-CSV-01 | `CsvWriter` | Value `=HYPERLINK(...)` | Prefixed with `'` |
| UT-CSV-02 | `CsvWriter` | Value with comma and quote | Properly quoted |
| UT-PCT-01 | Percentage calculator | Grand total zero | All percentages 0 |
| UT-PCT-02 | Percentage calculator | Three equal categories | Each 33.33 |

### 23.4 Integration test catalogue

Each test uses a fresh PostgreSQL container per test class (shared fixture), with data seeded by the test.

**Auth**

| ID | Scenario | Expected |
|---|---|---|
| IT-AUTH-01 | Register new user | 201, user has role `User` |
| IT-AUTH-02 | Register duplicate email, different case | 409 |
| IT-AUTH-03 | Register weak password | 400 with `password` key |
| IT-AUTH-04 | Login correct credentials | 200 with tokens |
| IT-AUTH-05 | Login wrong password 5 times | 6th attempt returns 423 |
| IT-AUTH-06 | Refresh with valid token | 200, old token now rejected |
| IT-AUTH-07 | Reuse rotated refresh token | 401, all user tokens revoked |
| IT-AUTH-08 | Logout then refresh | 401 |
| IT-AUTH-09 | Tampered JWT signature | 401 |
| IT-AUTH-10 | Expired JWT | 401 |
| IT-AUTH-11 | Forgot password unknown email | 202, same body as known email |
| IT-AUTH-12 | Rate limit on login | 6th request in a minute returns 429 |

**Isolation (the critical matrix)**

| ID | Scenario | Expected |
|---|---|---|
| IT-ISO-01 | User B lists expenses | Does not contain A's expenses |
| IT-ISO-02 | User B GET A's expense ID | 404 |
| IT-ISO-03 | User B PUT A's expense ID | 404, A's data unchanged |
| IT-ISO-04 | User B DELETE A's expense ID | 404, row still exists |
| IT-ISO-05 | User B creates expense using A's private category ID | 400 `categoryId` |
| IT-ISO-06 | User B reads A's category via GET categories | Not in list |
| IT-ISO-07 | User B summary | Totals exclude A's expenses |
| IT-ISO-08 | User B CSV export | No rows from A |
| IT-ISO-09 | Direct DB query bypassing filter via `IgnoreQueryFilters` in a test-only path | Only in explicit admin code paths; a test asserts they are not reachable from user endpoints |
| IT-ISO-10 | Non-admin calls every admin endpoint | 403 on each |

**Expenses**

| ID | Scenario | Expected |
|---|---|---|
| IT-EXP-01 | Create valid expense | 201, Location header, DTO correct |
| IT-EXP-02 | Create with amount 0 | 400 |
| IT-EXP-03 | Create with future date beyond 1 year | 400 |
| IT-EXP-04 | Update changes amount and sets UpdatedAtUtc | 200 |
| IT-EXP-05 | Delete then GET | 404 |
| IT-EXP-06 | List page 2 of 25 items with pageSize 20 | 5 items, totalPages 2 |
| IT-EXP-07 | Filter by category and amount range | Only matching rows |
| IT-EXP-08 | Sort by amount ascending | Ordered correctly |
| IT-EXP-09 | pageSize 500 | 400 |

**Categories**

| ID | Scenario | Expected |
|---|---|---|
| IT-CAT-01 | Create category with system name | 409 |
| IT-CAT-02 | Update system category | 403 |
| IT-CAT-03 | Delete category with expenses, no replacement | 400 `replacement_required` |
| IT-CAT-04 | Delete category with expenses and replacement | 204, expenses moved |
| IT-CAT-05 | Delete system category | 403 |

**Reports**

| ID | Scenario | Expected |
|---|---|---|
| IT-RPT-01 | Summary totals match seeded values | Exact match to 2 decimals |
| IT-RPT-02 | Summary with range over 366 days | 400 `range_too_large` |
| IT-RPT-03 | Monthly returns 12 entries with zeros | True |
| IT-RPT-04 | Empty range returns zero grand total and empty categories | True |
| IT-RPT-05 | Percentages sum within 0.02 of 100 | True |

**Admin**

| ID | Scenario | Expected |
|---|---|---|
| IT-ADM-01 | Admin lists users | 200, includes lock status |
| IT-ADM-02 | Admin locks user | That user's login returns 423 |
| IT-ADM-03 | Admin locks self | 400 `cannot_lock_self` |
| IT-ADM-04 | Admin totals JSON | No `userId` property anywhere |

**Security headers and errors**

| ID | Scenario | Expected |
|---|---|---|
| IT-SEC-01 | Any response | `X-Content-Type-Options: nosniff` present |
| IT-SEC-02 | Unhandled exception | 500 body has no stack trace |
| IT-SEC-03 | Correlation ID echoed | Same ID in response header |

### 23.5 Component test catalogue (bUnit)

| ID | Component | Scenario | Expected |
|---|---|---|---|
| UI-01 | ExpenseEditor | Empty submit | Shows required messages, no API call |
| UI-02 | ExpenseEditor | API returns 400 `amount` | Message appears under Amount |
| UI-03 | ExpenseEditor | Double-click submit | One API call |
| UI-04 | DoughnutChart | Data with three categories | JS interop called with 3 labels |
| UI-05 | DoughnutChart | Data empty | Empty state shown, no chart call |
| UI-06 | DateRangePicker | "This month" chip | Dates set to first and last of current month |
| UI-07 | Note rendering | Note `<script>alert(1)</script>` | Rendered as text, no script element |
| UI-08 | Login | 423 response | Message contains "temporarily locked" |
| UI-09 | Admin nav | Non-admin principal | Admin links not rendered |

### 23.6 Test data

- Builder pattern: `ExpenseBuilder`, `UserBuilder`, `CategoryBuilder` with sensible defaults.
- A `TestClock` implementing `IClock` with a fixed date for deterministic date rules.
- Seed fixture creates: two users (A and B), one admin, five expenses for A across two categories, three for B, one personal category each.

### 23.7 Coverage and quality gates

- Line coverage for Application and Features ≥ 75% (NFR-10).
- Mutation score for Domain and Features validators ≥ 70% (Stryker, run weekly in CI, not per commit).
- Zero warnings with `TreatWarningsAsErrors`.

---

## 24. Local development setup

### 24.1 Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |
| Docker Desktop or Docker Engine | Latest | `docker --version` |
| Node.js | Not required | (Chart.js loads from CDN) |
| EF Core CLI | 10.x | `dotnet tool install --global dotnet-ef` |
| Git | Latest | `git --version` |
| IDE | Visual Studio 2026, Rider, or VS Code with C# Dev Kit | |

### 24.2 First-time setup

```bash
# 1. Clone and enter the repo
git clone <your-repo-url> spendscope
cd spendscope

# 2. Start PostgreSQL
docker compose up -d postgres

# 3. Set user secrets (see section 22.2)

# 4. Restore and build
dotnet restore
dotnet build

# 5. Apply migrations
dotnet ef database update --project src/SpendScope.Infrastructure --startup-project src/SpendScope.Api

# 6. Run tests
dotnet test

# 7. Run the app
dotnet run --project src/SpendScope.Api
```

Open `https://localhost:7000` for the app and `https://localhost:7000/swagger` for the API docs (Development only).

### 24.3 docker-compose.yml (local)

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: spendscope
      POSTGRES_USER: spendscope
      POSTGRES_PASSWORD: localdev
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U spendscope"]
      interval: 5s
      retries: 10

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel.yaml"]
    volumes:
      - ./docs/observability/otel.yaml:/etc/otel.yaml:ro
    ports:
      - "4317:4317"

  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"

volumes:
  pgdata:
```

### 24.4 Common commands

| Task | Command |
|---|---|
| Add a migration | `dotnet ef migrations add <Name> --project src/SpendScope.Infrastructure --startup-project src/SpendScope.Api` |
| Remove last migration | `dotnet ef migrations remove --project ... --startup-project ...` |
| Reset database | `dotnet ef database drop -f` then `database update` |
| Run architecture tests | `dotnet test tests/SpendScope.ArchitectureTests` |
| Run all tests with coverage | `dotnet test --collect:"XPlat Code Coverage"` |
| Publish Web into API | `dotnet publish src/SpendScope.Api -c Release` (copies Web build into wwwroot) |

---

## 25. Containerization and deployment

### 25.1 Dockerfile (multi-stage)

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/SpendScope.Api/SpendScope.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN useradd -m appuser
USER appuser
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "SpendScope.Api.dll"]
```

Notes:
- Runs as a non-root user.
- Only the publish output is in the final image.
- Health check uses the live endpoint.

### 25.2 Production compose (example)

Add an `api` service that builds the Dockerfile, depends on `postgres` with `condition: service_healthy`, and receives secrets as environment variables. Use a reverse proxy (Caddy or Nginx) for TLS in front of the API.

### 25.3 Deployment targets

| Option | When to use |
|---|---|
| Azure Container Apps | Recommended for this project. Scales to zero, managed TLS, managed PostgreSQL (Azure Database for PostgreSQL Flexible Server). |
| Render or Fly.io | Simple alternative for a portfolio demo. |
| Single VPS with Docker Compose | Cheapest, good for learning Caddy and Linux operations. |

### 25.4 Database migration in production

- Run `dotnet ef migrations bundle` in CI to produce a single executable.
- Run the bundle as a separate step before deploying the new API revision.
- Never auto-migrate on app startup in production.

---

## 26. CI/CD pipeline

### 26.1 Pipeline stages

```
push / PR
   │
   ├─► restore + build (warnings as errors)
   ├─► architecture tests
   ├─► unit tests + coverage
   ├─► component tests (bUnit)
   ├─► integration tests (Testcontainers)
   ├─► security scan (dotnet list package --vulnerable --include-transitive)
   ├─► secret scan (gitleaks)
   │
   └─► (main branch only)
         ├─► build Docker image, tag with commit SHA
         ├─► push to container registry
         ├─► generate migration bundle
         └─► deploy to staging, run smoke tests
               └─► manual approval ─► deploy to production
```

### 26.2 GitHub Actions outline

```yaml
name: ci
on:
  push:
    branches: [main]
  pull_request:

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      docker:
        image: docker:dind
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -warnaserror
      - run: dotnet test --no-build --filter "FullyQualifiedName~ArchitectureTests"
      - run: dotnet test --no-build --filter "FullyQualifiedName!~IntegrationTests" --collect:"XPlat Code Coverage"
      - run: dotnet test --no-build --filter "FullyQualifiedName~IntegrationTests"
      - run: dotnet list package --vulnerable --include-transitive

  image:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - uses: actions/checkout@v4
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - uses: docker/build-push-action@v6
        with:
          push: true
          tags: ghcr.io/${{ github.repository_owner }}/spendscope:${{ github.sha }}
```

GitHub-hosted runners support Docker, so Testcontainers works without `dind`. Remove the `services.docker` block if you use `ubuntu-latest` directly.

### 26.3 Branching and commits

- Trunk-based: short-lived feature branches, PR into `main`.
- Conventional commits: `feat(expenses): add CSV export`, `fix(auth): revoke tokens on reuse`.
- PRs require green CI and one approval (self-approval acceptable for solo learning, but still review the diff).

---

## 27. Milestones and task list

Each milestone ends with a demo and a passing test set. Estimated effort assumes part-time work (about 10 hours per week).

### Milestone 0: Foundations (Week 1)
- [ ] Create solution with the folder layout from section 11.
- [ ] Add `Directory.Build.props` and `Directory.Packages.props`.
- [ ] Add `.editorconfig` with the team's naming rules.
- [ ] Add ArchitectureTests with ARCH-01 to ARCH-09 (initially empty namespaces pass).
- [ ] Add docker-compose with PostgreSQL. Verify connection.
- [ ] Add Serilog and a health check endpoint.
- **Demo:** `/health/ready` returns healthy.

### Milestone 1: Identity and auth (Weeks 2 to 3)
- [ ] Configure Identity with PostgreSQL, password policy, lockout.
- [ ] Add JWT issuance and validation with startup key check.
- [ ] Implement Register, Login, Refresh (with rotation and reuse detection), Logout.
- [ ] Seed roles and admin user in development.
- [ ] Rate limiting on auth endpoints.
- [ ] Integration tests IT-AUTH-01 to IT-AUTH-12.
- **Demo:** Swagger flow: register, login, refresh, logout.

### Milestone 2: Expenses and isolation (Weeks 4 to 5)
- [ ] Domain: `Expense`, `Money`, invariants and `IClock`.
- [ ] Infrastructure: `AppDbContext`, configurations, global query filter, migration.
- [ ] Slices: CreateExpense, GetExpense, UpdateExpense, DeleteExpense, ListExpenses.
- [ ] Unit tests UT-DOM and UT-VAL.
- [ ] Integration tests IT-EXP and IT-ISO-01 to IT-ISO-04.
- **Demo:** Two users, isolation visible in Swagger.

### Milestone 3: Categories (Week 6)
- [ ] System category seeding.
- [ ] Slices: ListCategories, CreateCategory, UpdateCategory, DeleteCategory with reassignment transaction.
- [ ] Integration tests IT-CAT and IT-ISO-05 to IT-ISO-06.
- **Demo:** Delete category with reassignment.

### Milestone 4: Reports (Week 7)
- [ ] Summary and monthly report slices with database-side aggregation.
- [ ] CSV export slice with injection protection.
- [ ] Performance seed script (100k rows) and `EXPLAIN ANALYZE` notes in `docs/perf/`.
- [ ] Integration tests IT-RPT and IT-ISO-07 to IT-ISO-08.
- **Demo:** Summary JSON for a seeded dataset.

### Milestone 5: Admin (Week 8)
- [ ] List users, lock and unlock, system totals.
- [ ] Integration tests IT-ADM and IT-ISO-10.
- [ ] Verify admin responses contain no expense rows (IT-ADM-04).
- **Demo:** Admin locks a user, user cannot log in.

### Milestone 6: Blazor frontend, shell and auth (Weeks 9 to 10)
- [ ] Create hosted Blazor WASM project, wire into API.
- [ ] MudBlazor setup, theme, layout, nav menu.
- [ ] JwtAuthStateProvider, TokenStore, auth handlers, refresh flow.
- [ ] Login, Register, Profile pages.
- [ ] bUnit tests for Login (UI-08) and Admin nav (UI-09).
- **Demo:** Log in, see authorized nav, log out.

### Milestone 7: Blazor expenses and categories (Weeks 11 to 12)
- [ ] Expense list with server paging, sorting, filters.
- [ ] Expense editor with server validation mapping.
- [ ] Category manager with color picker and reassignment dialog.
- [ ] CSV export button.
- [ ] bUnit tests UI-01 to UI-03, UI-07.
- **Demo:** Full expense lifecycle in the browser.

### Milestone 8: Dashboard and charts (Weeks 13 to 14)
- [ ] Date range picker with URL state.
- [ ] Summary cards, doughnut chart, category table, monthly bar chart.
- [ ] Chart.js interop and disposal.
- [ ] Empty states and skeleton loaders.
- [ ] bUnit tests UI-04 to UI-06.
- **Demo:** Dashboard reflects new expense within one refresh.

### Milestone 9: Hardening (Week 15)
- [ ] Security headers and CSP.
- [ ] Correlation IDs in logs and responses.
- [ ] Concurrency tokens and If-Match on expense update.
- [ ] Accessibility pass: keyboard navigation, labels, contrast.
- [ ] Verify all items in section 21 checklist.
- **Demo:** Security checklist walkthrough.

### Milestone 10: Delivery (Week 16)
- [ ] Dockerfile and production compose.
- [ ] GitHub Actions pipeline with all stages.
- [ ] Deploy to a staging environment.
- [ ] README with screenshots, architecture diagram, and setup steps.
- [ ] Record a 3-minute demo video.
- **Demo:** Public staging URL.

---

## 28. Definition of done

A feature is done when all of the following are true:

- [ ] Requirement IDs it satisfies are listed in the PR description.
- [ ] Code follows the slice structure and passes architecture tests.
- [ ] Unit, integration, and component tests exist for the listed test IDs and pass.
- [ ] Authorization is enforced at the endpoint and, for single resources, in the handler.
- [ ] A negative test exists for every authorization rule.
- [ ] Validation errors map to the field names used by the client.
- [ ] Logs contain no sensitive data.
- [ ] OpenAPI description is accurate (summaries, response types, status codes).
- [ ] UI has loading, empty, and error states.
- [ ] Keyboard navigation and labels work on the new screens.
- [ ] Migration (if any) is reviewed and tested against a fresh database.
- [ ] README or docs updated if behavior changed.

---

## 29. Risks and mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Developer forgets user filter in a new query | Medium | Critical | Global query filter plus integration matrix plus code review checklist. |
| Refresh token theft via XSS | Low to medium | High | CSP, Blazor escaping, planned cookie migration (v1.1). |
| Decimal rounding causes chart and table mismatch | Medium | Low | Compute totals in SQL, format at display only, add footnote. |
| Scope creep (budgets, multi-currency) | High | Medium | Strict scope list in section 2. Park ideas in section 30. |
| Blazor WASM download size slows first load | Medium | Medium | AOT only for release, trimming, lazy loading admin pages. |
| Architecture rules erode over time | Medium | High | Architecture tests fail the build. |
| Chart.js CDN unavailable | Low | Medium | Pin version and self-host `chart.umd.min.js` in `wwwroot/lib` as a fallback. |
| Time zone confusion for dates | Medium | Low | Use `DateOnly` for user-facing dates. Store UTC only for audit timestamps. |
| Token clock skew across machines | Low | Low | 30-second skew allowance. |
| Testcontainers slow in CI | Medium | Low | Share one container per test collection. Cache Docker layers. |

---

## 30. Future enhancements

Ordered by suggested priority.

1. **HttpOnly refresh cookie** (v1.1). Moves refresh token out of JavaScript reach.
2. **Monthly budgets per category** with progress bars and alerts when 80% reached.
3. **Recurring expenses** (rent, subscriptions) generated automatically.
4. **Receipt attachments** stored in Azure Blob Storage or local volume, with size and type checks.
5. **Multi-currency** with stored exchange rate per expense date.
6. **Import from CSV** with column mapping and preview.
7. **Email verification** and password reset emails via SMTP or a provider.
8. **Access token denylist** in Redis keyed by `jti`, for immediate logout.
9. **Audit log** of admin actions, using domain events.
10. **Web push or email digests** of weekly spending.
11. **Localization** of UI strings and number and date formats.
12. **Offline entry** in the Blazor client with a sync queue.

---

## 31. Interview talking points

Prepare short answers (60 to 90 seconds each) for these questions. Each is grounded in a decision made in this spec.

| Question | Points to cover |
|---|---|
| Why Identity instead of a custom user table? | Password hashing, lockout, and token providers are security-sensitive and well tested. Writing them yourself is a common source of vulnerabilities. |
| How do you prevent users from seeing each other's data? | Three layers: query scoping from the token, global query filter as a safety net, and resource-based authorization. Plus integration tests for every isolation scenario. |
| Why return 404 instead of 403? | 403 confirms the resource exists. 404 reveals nothing. |
| How does refresh token rotation work and what is reuse detection? | Each refresh invalidates the previous token. Presenting an old token means theft is likely, so all sessions are revoked. |
| What is the trade-off with JWTs and logout? | Access tokens remain valid until expiry. Short lifetime limits the window. Denylist is the option if immediate revocation is required. |
| Why layers and slices together? | Layers control dependency direction. Slices keep features cohesive. Architecture tests keep both honest. |
| Why not MediatR? | It adds an indirection layer that isn't needed for this scope. The dispatcher is a few lines and keeps handlers explicit. |
| Why aggregate in the database? | Loading rows to sum them in memory doesn't scale and sends personal data over the wire unnecessarily. |
| Why PostgreSQL over SQLite? | `numeric` aggregation translates correctly. SQLite's decimal handling breaks `Sum` in EF Core. |
| Why Blazor WASM hosted with the API? | Same origin avoids CORS, one artifact to deploy, and shared Contracts project keeps DTOs in sync at compile time. |
| How do you test a Blazor component? | bUnit renders components with fake services, so you verify behavior without a browser. |
| What would you change for scale? | Read replicas for reports, caching summaries, moving CSV export to a background job, and partitioning expenses by year if volume grows. |

---

## Appendix A: Code reference

### A.1 Program.cs (Api, composition root, abbreviated)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddFeatures()
    .AddJwtAuthentication(builder.Configuration)
    .AddSpendScopeAuthorization()
    .AddSpendScopeRateLimiting()
    .AddSpendScopeHealthChecks()
    .AddOpenTelemetryConfig(builder.Configuration)
    .AddProblemDetails()
    .AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseCorrelationId();
app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>(); // optional, see note
app.MapFeatureEndpoints(typeof(IFeatureEndpoint).Assembly);
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapFallbackToFile("index.html");

if (app.Environment.IsDevelopment())
{
    await app.Services.SeedAsync(app.Configuration);
    app.MapOpenApi();
}

app.Run();
```

> **Note on `MapIdentityApi`:** It is a quick way to get register and login. Because this project issues its own refresh tokens, many teams replace it with custom slices (sections 15.1 to 15.4). Build the custom version for the learning goal, and compare it with the built-in one in your README.

### A.2 IFeatureEndpoint and discovery

```csharp
public interface IFeatureEndpoint
{
    void Map(IEndpointRouteBuilder app);
}

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapFeatureEndpoints(
        this IEndpointRouteBuilder app, Assembly assembly)
    {
        var endpoints = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                     && typeof(IFeatureEndpoint).IsAssignableFrom(t))
            .Select(Activator.CreateInstance)
            .Cast<IFeatureEndpoint>();

        foreach (var endpoint in endpoints)
            endpoint.Map(app);

        return app;
    }
}
```

### A.3 Example slice: CreateExpense

**CreateExpenseRequest.cs**

```csharp
namespace SpendScope.Features.Expenses.CreateExpense;

public sealed record CreateExpenseRequest(
    decimal Amount,
    DateOnly OccurredOn,
    Guid CategoryId,
    string? Note);
```

**CreateExpenseValidator.cs**

```csharp
public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseValidator(IClock clock, IOptions<ExpenseOptions> options)
    {
        var max = options.Value.MaxAmount;
        var maxFuture = clock.Today.AddDays(options.Value.MaxFutureDays);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithErrorCode("amount_range")
            .LessThanOrEqualTo(max).WithErrorCode("amount_range")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithErrorCode("amount_precision");

        RuleFor(x => x.OccurredOn)
            .LessThanOrEqualTo(_ => clock.Today.AddDays(options.Value.MaxFutureDays))
            .WithErrorCode("date_future");

        RuleFor(x => x.CategoryId).NotEmpty();

        RuleFor(x => x.Note)
            .MaximumLength(500).WithErrorCode("note_length")
            .When(x => x.Note is not null);
    }
}
```

> `PrecisionScale` is a FluentValidation rule for decimal values. Verify the method name against the version you install, because it has changed across releases.

**CreateExpenseHandler.cs**

```csharp
public sealed class CreateExpenseHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<Result<ExpenseDto>> HandleAsync(
        CreateExpenseRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId;

        var category = await db.Categories
            .Where(c => c.Id == request.CategoryId && (c.IsSystem || c.UserId == userId))
            .Select(c => new { c.Id, c.Name, c.ColorHex })
            .FirstOrDefaultAsync(ct);

        if (category is null)
            return Result<ExpenseDto>.Fail(new Error(
                "category_not_found", "Category does not exist.", ErrorType.Validation));

        var currency = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.DefaultCurrency)
            .SingleAsync(ct);

        var expense = Expense.Create(
            userId,
            new Money(request.Amount, currency),
            request.OccurredOn,
            category.Id,
            request.Note,
            clock.UtcNow);

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        return Result<ExpenseDto>.Ok(new ExpenseDto(
            expense.Id,
            expense.Amount,
            expense.Currency,
            expense.OccurredOn,
            new CategoryRef(category.Id, category.Name, category.ColorHex),
            expense.Note,
            expense.CreatedAtUtc));
    }
}
```

**CreateExpenseEndpoint.cs**

```csharp
public sealed class CreateExpenseEndpoint : IFeatureEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/expenses", async (
                CreateExpenseRequest request,
                IValidator<CreateExpenseRequest> validator,
                CreateExpenseHandler handler,
                CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
                return Results.ValidationProblem(validation.ToDictionary());

            var result = await handler.HandleAsync(request, ct);
            return result.ToHttpResult(
                onSuccess: dto => Results.Created($"/api/expenses/{dto.Id}", dto));
        })
        .RequireAuthorization("RequireUser")
        .WithName("CreateExpense")
        .WithSummary("Create an expense for the signed-in user.")
        .Produces<ExpenseDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
```

`ToHttpResult` is an extension that maps `ErrorType` to status codes (section 10.6). Put it in `Api/Endpoints/ResultExtensions.cs`.

### A.4 Global query filter wiring

```csharp
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<Expense>()
            .HasQueryFilter(e => e.UserId == currentUser.UserId);

        builder.Entity<Category>()
            .HasQueryFilter(c => c.IsSystem || c.UserId == currentUser.UserId);
    }
}
```

> **Design note:** `currentUser.UserId` must be resolved per request. Register `ICurrentUser` as scoped, and make it return `Guid.Empty` (or throw) when there is no user. For background jobs and admin tools that need unfiltered access, use an explicit `IgnoreQueryFilters()` in a dedicated, reviewed code path.

### A.5 Blazor program bootstrap (Web)

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<AuthorizationHeaderHandler>();
builder.Services.AddTransient<RefreshOnUnauthorizedHandler>();

builder.Services.AddHttpClient<AuthApiClient>(c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<ExpenseApiClient>(c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<AuthorizationHeaderHandler>()
    .AddHttpMessageHandler<RefreshOnUnauthorizedHandler>();

builder.Services.AddScoped<ChartInterop>();

await builder.Build().RunAsync();
```

Register the other clients (`CategoryApiClient`, `ReportApiClient`, `AdminApiClient`) the same way.

### A.6 Shared Contracts example

```csharp
namespace SpendScope.Contracts.Expenses;

public sealed record ExpenseDto(
    Guid Id,
    decimal Amount,
    string Currency,
    DateOnly OccurredOn,
    CategoryRef Category,
    string? Note,
    DateTime CreatedAtUtc);

public sealed record CategoryRef(Guid Id, string Name, string ColorHex);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

Both the API and Blazor reference this project, so a DTO change becomes a compile error in both places.

---

## Document history

| Version | Date | Change |
|---|---|---|
| 1.0 | Initial | Full specification: requirements, architecture, API, testing, frontend, delivery plan. |
