# board_third-party-lib_backend

A backend service for third party developers for the Board ecosystem to use to register and share their games with the public.

Current implementation status:

- implemented now: health endpoints, Keycloak-backed identity/auth foundation, Wave 1 persistence for `users` and `user_board_profiles`, Wave 2 organizations and memberships, and Waves 3 through 5 title/catalog and acquisition persistence
- planned next: Wave 6 unified commerce and entitlements

## Table of Contents

- [Local development (Phase 2)](#local-development-phase-2)
- [Planning](#planning)
- [API Testing (Postman)](#api-testing-postman)

## Local development (Phase 2)

Prereq: local PostgreSQL and Keycloak running (see [`backend/docker-compose.yml`](docker-compose.yml)).

Recommended (repo root, automated):

```bash
python ./scripts/dev.py bootstrap
python ./scripts/dev.py up
```

Backend-only test commands (repo root, automated):

```bash
python ./scripts/dev.py test
python ./scripts/dev.py test --skip-integration
```

See the root developer CLI doc for full command help and DB backup/restore helpers:

- [`docs/developer-cli.md`](../docs/developer-cli.md)

Run the API:

```bash
dotnet restore
dotnet run --project src/Board.ThirdPartyLibrary.Api
```

Verify endpoints:

```bash
curl http://localhost:5085/health/live
curl http://localhost:5085/health/ready
curl http://localhost:5085/identity/auth/config
curl http://localhost:5085/organizations
curl http://localhost:5085/catalog
```

Notes:

- `appsettings.Development.json` is preconfigured for the local Postgres container and local Keycloak realm import.
- Override with env var `ConnectionStrings__BoardLibrary` if needed.
- Override Keycloak settings with `Authentication__Keycloak__*` environment variables if needed.
- Authentication data ownership is documented in [`backend/docs/auth-data-ownership.md`](docs/auth-data-ownership.md).
- Current catalog/title schema behavior is documented in [`backend/docs/title-catalog-schema.md`](docs/title-catalog-schema.md).

Local Keycloak bootstrap defaults:

- Keycloak admin console: `http://localhost:8080/admin/`
- Keycloak bootstrap admin: `admin` / `admin`
- Seeded realm user for login testing: `local-admin` / `ChangeMe!123`

To verify the browser login flow locally, open:

```bash
http://localhost:5085/identity/auth/login
```

Keycloak will host the login/registration UI and redirect back to:

```bash
http://localhost:5085/identity/auth/callback
```

## Planning

Backend schema implementation is planned as EF Core code-first with migrations as the database schema source of truth.

Authentication and platform-role state are intentionally excluded from the application database source of truth and remain Keycloak-owned.

See:

- [`backend/planning/mvp-schema-implementation-plan.md`](planning/mvp-schema-implementation-plan.md)
- [`planning/current-state-and-wave-plan.md`](../planning/current-state-and-wave-plan.md)
- [`backend/docs/title-catalog-schema.md`](docs/title-catalog-schema.md)

## API Testing (Postman)

The maintained contract-test and environment assets now live in the `api` submodule and are executed through the root developer CLI.

See:

- [`api/README.md`](../api/README.md)
- [`backend/docs/postman-api-testing.md`](docs/postman-api-testing.md)
