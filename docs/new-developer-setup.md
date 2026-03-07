# New Developer Setup (API-First Backend MVP)

This guide is for getting a new developer to a working local backend environment quickly.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start (Closest to Single Operation)](#quick-start-closest-to-single-operation)
- [VS Code Setup (Recommended)](#vs-code-setup-recommended)
- [Common Commands](#common-commands)
- [Manual Equivalents (If You Prefer Not to Use Scripts)](#manual-equivalents-if-you-prefer-not-to-use-scripts)
- [Project Layout (Current)](#project-layout-current)
- [Troubleshooting](#troubleshooting)

## Prerequisites

Install these on your machine:

1. **Git**
2. **Docker Desktop** (or Docker Engine with Compose support)
3. **.NET SDK 10.x** (the backend is currently pinned and targeted to `.NET 10` / `net10.0`)
4. **Python 3.10+** (required for the root developer CLI: `scripts/dev.py`)

Optional but useful:

- VS Code / Visual Studio / Rider
- `curl` for quick endpoint checks

## VS Code Setup (Recommended)

This repository includes shared VS Code workspace configuration in the root `/.vscode/` folder for backend development.

Included config:

- Launch profiles for backend **Debug** and **Release** runs
- Tasks that start/reuse PostgreSQL dependencies and build the backend
- Recommended extensions in [`/.vscode/extensions.json`](../../.vscode/extensions.json)

Recommended workflow in VS Code:

1. Open the repo root workspace (`board-enthusiasts`).
2. Open **Run and Debug** (`Ctrl+Shift+D`).
3. Select `Backend API (Debug Build)` or `Backend API (Release Build)`.
4. Press `F5`.

The configured pre-launch tasks will:

- start/reuse the `board_tpl_postgres`, `board_tpl_mailpit`, and `board_tpl_keycloak` containers
- build the backend in the selected configuration
- launch the API in the integrated terminal

### .NET HTTPS development certificate prompt (manual step)

When running/debugging .NET apps in VS Code, you may see a security prompt asking to create/trust a development certificate.

- This is a normal **manual OS trust** step for .NET local development.
- It may require OS/admin confirmation and cannot be fully automated by repo config.
- The current backend launch path is HTTP-based, but tooling may still prompt for the cert.

If prompted, approve it. You can also run this once manually:

```powershell
dotnet dev-certs https --trust
```

Or run the VS Code task:

- `tpl: trust dotnet dev certificate`

## Quick Start (Closest to Single Operation)

From the repository root:

```powershell
python ./scripts/dev.py bootstrap
python ./scripts/dev.py up
```

What this does:

- `bootstrap`: initializes git submodules (if needed) and restores the backend solution
- `up`: starts local PostgreSQL, Mailpit, and Keycloak (or reuses existing `board_tpl_postgres` / `board_tpl_mailpit` / `board_tpl_keycloak` containers) and runs the backend API
- together, these commands provide a reliable first-time setup + startup flow
- for convenience, `python ./scripts/dev.py up --bootstrap` is supported

For full command coverage (including DB backup/restore helpers), see:

- [`docs/developer-cli.md`](../../docs/developer-cli.md)

When the API is running, verify:

```powershell
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/health/live
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/health/ready
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/identity/auth/config
```

Current persistence note:

- PostgreSQL is now used for backend readiness plus Wave 1 through Wave 5 persistence.
- Keycloak owns authentication data, platform roles, and login/account lifecycle flows.
- The maintained current API surface includes persisted Board profile CRUD, studios/memberships, titles/versioned metadata, media assets, release history, APK artifact metadata, supported publishers, and external acquisition bindings backed by PostgreSQL.
- See [`backend/docs/auth-data-ownership.md`](auth-data-ownership.md) for the current data ownership boundary.
- See [`backend/docs/title-catalog-schema.md`](title-catalog-schema.md) for the current title/catalog schema and lifecycle model.

## Common Commands

The root developer CLI command catalog (bootstrap/up/down/status/test/verify/doctor and DB backup/restore helpers) is documented in:

- [`docs/developer-cli.md`](../../docs/developer-cli.md)

Common backend examples:

```powershell
python ./scripts/dev.py doctor
python ./scripts/dev.py up --dependencies-only
python ./scripts/dev.py status
python ./scripts/dev.py verify --skip-contract-tests
python ./scripts/dev.py test
python ./scripts/dev.py test --skip-integration
```

Run the maintained API contract tests:

```powershell
python ./scripts/dev.py api-test
```

## Manual Equivalents (If You Prefer Not to Use Scripts)

Initialize submodules:

```powershell
git submodule update --init --recursive
```

Restore backend solution:

```powershell
dotnet restore ./backend/Board.ThirdPartyLibrary.Backend.sln
```

Start PostgreSQL:

```powershell
docker compose -f ./backend/docker-compose.yml up -d postgres
docker compose -f ./backend/docker-compose.yml up -d mailpit
docker compose -f ./backend/docker-compose.yml up -d keycloak
```

Run backend API:

```powershell
dotnet run --project ./backend/src/Board.ThirdPartyLibrary.Api/Board.ThirdPartyLibrary.Api.csproj
```

## Project Layout (Current)

- `backend/`: ASP.NET Core API, tests, backend CI workflow
- `frontend/`: frontend submodule (MAUI client work planned)
- [`docs/`](../../docs): project-wide developer documentation (root repo)
- [`planning/`](../../planning): project-wide planning and recommendation artifacts (root repo)
- [`backend/docs/`](../docs): backend-specific setup and usage docs (this submodule)
- [`backend/planning/`](../planning): backend planning and implementation-tracking artifacts
- [`api/postman/`](../../api/postman): maintained OpenAPI, contract-test collections, and environment templates
- [`backend/postman/`](../postman): legacy/local-only backend Postman assets
- [`scripts/`](../../scripts): root-level developer orchestration scripts

## Troubleshooting

### Port 5432 already in use

Update the host port mapping in `backend/docker-compose.yml` (for example `5433:5432`) and update the backend connection string accordingly:

- `backend/src/Board.ThirdPartyLibrary.Api/appsettings.Development.json`
- or environment variable `ConnectionStrings__BoardLibrary` (keep `SSL Mode=VerifyFull` for the local TLS-enabled Postgres setup)

### Docker container starts but readiness fails

Check container logs:

```powershell
docker logs board_tpl_postgres
docker logs board_tpl_mailpit
docker logs board_tpl_keycloak
```

### Script says a command is missing

Run:

```powershell
python ./scripts/dev.py doctor
```

Then install the missing prerequisite and rerun the quick-start command.

### Container name conflict for `board_tpl_postgres`

The automation script now attempts to reuse an existing `board_tpl_postgres` container before creating a new one with Docker Compose.

If you still want to reset it completely:

```powershell
docker stop board_tpl_postgres
docker rm board_tpl_postgres
python ./scripts/dev.py up
```

### Local Keycloak defaults

The local compose file imports a development realm for repeatable auth testing.

- Keycloak admin console: [`https://localhost:8443/admin/`](https://localhost:8443/admin/)
- Keycloak bootstrap admin credentials: `admin` / `admin`
- Seeded local realm user credentials: `local-admin` / `ChangeMe!123`
- Local verification email inbox: [`https://localhost:8025`](https://localhost:8025)

To exercise the backend login flow locally:

1. Start the stack with `python ./scripts/dev.py up`.
2. Open `https://localhost:7085/identity/auth/login` in a browser.
3. Sign in with the seeded realm user, or register a new user from the hosted Keycloak screen.
4. If you register a new user, open the verification email in Mailpit at [`https://localhost:8025`](https://localhost:8025).
5. Confirm the callback response at `https://localhost:7085/identity/auth/callback`.
6. Use the returned access token against `GET /identity/me`.

### VS Code warns about HTTPS development certificate

This is expected for .NET development tooling on some machines.

- Approve the prompt, or run `dotnet dev-certs https --trust` manually once.
- You can also run the VS Code task: `tpl: trust dotnet dev certificate`.

See also:

- [Auth and data ownership (`backend/docs/auth-data-ownership.md`)](auth-data-ownership.md)
- [Title catalog schema (`backend/docs/title-catalog-schema.md`)](title-catalog-schema.md)
- [Technology direction (`planning/technology-fit-recommendation.md`)](../../planning/technology-fit-recommendation.md)
- [Phase 1 Postgres setup (`backend/docs/backend-phase-1-postgres-setup.md`)](backend-phase-1-postgres-setup.md)
- [Postman API testing (`backend/docs/postman-api-testing.md`)](postman-api-testing.md)

