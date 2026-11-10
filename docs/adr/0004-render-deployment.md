# ADR 0004: Deploying to Render

- **Status:** Accepted
- **Date:** 2026-11-11

## Context

SpendScope is deployed to Render from the `master` branch of
github.com/sreeragbuilds/SpendScope. Render has no native .NET runtime, so the app runs as a
Docker web service next to a Render PostgreSQL database.

## Decisions

| Topic | Decision | Why |
|---|---|---|
| Runtime | Docker web service built from the repo's `Dockerfile` (multi-stage, non-root `app` user, spec 25.1). | No native .NET runtime on Render. |
| Port | The container listens on `$PORT` (Render sets it), defaulting to 8080. | Render routes traffic to the port it chooses. |
| Database URL | The app accepts `DATABASE_URL` (`postgresql://…`) and converts it to an Npgsql connection string; `ConnectionStrings__Default` still wins when set. | Render, like most platforms, hands out databases as URLs. |
| TLS and client IPs | `ForwardedHeaders__Enabled=true` makes the app trust `X-Forwarded-For`/`-Proto` from the **last hop only** (Render's proxy). | Without it every client shares the proxy's IP, so all users would share one login rate limit; trusting only one hop stops clients spoofing their IP. |
| Migrations | `Database__MigrateOnStartup=true` on Render. | Spec 25.4 prefers a separate migration step, but Render's free plan has no pre-deploy command. With a single instance there is no concurrent-migration race. On a paid plan, switch to a pre-deploy `dotnet ef database update` (or a migration bundle) and turn this off. |
| Data Protection keys | Stored in PostgreSQL (`DataProtectionKeys` table, migration `AddDataProtectionKeys`). | The container filesystem is wiped on every restart (the free plan restarts after idling), which would invalidate outstanding password reset links. |
| Hosted `index.html` | The image publishes `SpendScope.Web` separately and uses its processed `index.html`; a build step fails if a placeholder or missing file remains. | Publishing the hosting Api copies Web's *unprocessed* `index.html` (`blazor.webassembly#[.{fingerprint}].js`, empty import map), so the client never started. Found by running the image locally before deploying. |
| Email | No provider yet: production logs "Email not sent" without the message body. | Reset tokens must never reach logs. Password reset by email does not work until SMTP is added. |

## Consequences

- Free web services sleep after 15 minutes without traffic; the next request waits for a cold start.
- Render's free PostgreSQL expires after 30 days unless upgraded. Data is lost when it does.
- Data Protection keys are stored unencrypted at rest in the database (the warning
  "No XML encryptor configured" is expected). Acceptable for a demo; use a key vault or
  certificate-based encryption for real users.
