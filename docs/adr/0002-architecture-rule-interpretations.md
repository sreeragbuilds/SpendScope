# ADR 0002: How the architecture rules are interpreted and enforced

- **Status:** Accepted
- **Date:** 2026-11-11

## Context

Spec section 10.3 defines ARCH-01 to ARCH-09. Several rules conflict with other parts of the
spec, or are ambiguous, once they are turned into automated checks. The tests live in
`tests/SpendScope.ArchitectureTests` and use NetArchTest.Rules.

## Decisions

| Rule | Interpretation | Why |
|---|---|---|
| ARCH-01 | Checked twice: Domain types may only depend on `System.*` and Domain, and the Domain assembly may only reference BCL assemblies. Compiler-emitted types (`System.Runtime.CompilerServices`, `Microsoft.CodeAnalysis`) are excluded. | The assembly check catches a stray package reference even when only one attribute from it is used. |
| ARCH-04 | A slice is the namespace `SpendScope.Features.{Area}.{Slice}`. Types directly under `SpendScope.Features` or `SpendScope.Features.{Area}` are shared and allowed. | Matches the folder layout in spec section 11. NetArchTest matches whole namespace segments, so `CreateExpense` is not confused with `CreateExpenseCsv`. |
| ARCH-07 | Classes named `*Handler` must be in `SpendScope.Features.*`, **except** classes that implement `IExceptionHandler` or `IAuthorizationHandler`, or inherit `HttpMessageHandler`. | The spec itself places `GlobalExceptionHandler` in Api, an `OwnerRequirement` authorization handler, and `AuthorizationHeaderHandler` / `RefreshOnUnauthorizedHandler` in Web. The rule is about feature handlers. |
| ARCH-08 | `IFeatureEndpoint` lives in **Features** (namespace `SpendScope.Features`), not `Api/Endpoints` as section 11 shows. Discovery (`MapFeatureEndpoints`) stays in Api. | ARCH-08 requires implementations to live in Features, and ARCH-03 forbids Features from referencing Api. |
| ARCH-09 | EF Core is forbidden in Domain, Contracts and Web. In Application it is allowed only in `SpendScope.Application.Abstractions` (for `IAppDbContext` and its `DbSet<T>`). In Features it is allowed only in `*Handler` classes, and no Features type may use `DbContext` directly. | The spec's own `IAppDbContext` and `CreateExpenseHandler` (appendix A.3, A.4) need EF types in Application and Features. This keeps EF inside a small, reviewable perimeter. |

Extra rules beyond the spec:

- **Contracts isolation:** Contracts references no other SpendScope project (spec 10.2).
- **Api does not use Web types:** see ADR 0001.

## Verification

Each rule was checked against a temporary planted violation (removed afterwards). Every rule
failed with a message naming the rule ID and the offending type, and a Blazor
`DelegatingHandler` named `*Handler` was correctly *not* reported by ARCH-07.

## Consequences

- Rules pass trivially while the projects are nearly empty. They start to matter as slices land.
- ARCH-09's Features check keys off the `Handler` name suffix, so a handler must be named `*Handler`.
