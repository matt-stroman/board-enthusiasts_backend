# board_third-party-lib_backend

A backend service for third party developers for the Board ecosystem to use to register and share their games with the public.

Current implementation status:

- implemented now: health endpoints, Keycloak-backed identity/auth foundation, review-based developer enrollment plus moderator approval, Wave 1 persistence for `users` and `user_board_profiles`, user profile and avatar management endpoints, Wave 2 organizations and memberships, and Waves 3 through 5 title/catalog and acquisition persistence
- planned next: Wave 6 unified commerce and entitlements

## Table of Contents

- [Local development (Phase 2)](#local-development-phase-2)
- [Planning](#planning)
- [API Testing (Postman)](#api-testing-postman)

## Local development (Phase 2)

Prereq: local PostgreSQL, Mailpit, and Keycloak running (see [`backend/docker-compose.yml`](docker-compose.yml)).

Recommended (repo root, automated):

```bash
python ./scripts/dev.py bootstrap
python ./scripts/dev.py up
```

Backend-only test commands (repo root, automated):

```bash
python ./scripts/dev.py verify --skip-contract-tests
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
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/health/live
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/health/ready
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/identity/auth/config
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/organizations
Invoke-WebRequest -SkipCertificateCheck -HttpVersion 2.0 https://localhost:7085/catalog
```

Notes:

- `appsettings.Development.json` is preconfigured for the local Postgres container with TLS enabled and the local Keycloak realm import.
- Override with env var `ConnectionStrings__BoardLibrary` if needed.
- Override Keycloak settings with `Authentication__Keycloak__*` environment variables if needed.
- Title media uploads (`POST /developer/titles/{titleId}/media/{mediaRole}/upload`) use local filesystem storage by default at `artifacts/title-media` and are served publicly under `/uploads/title-media/*`.
- Upload validation currently allows JPEG/PNG/WEBP/GIF up to 25 MB per file.
- Override storage root with `TitleMediaStorage__RootPath` when needed.
- Authentication data ownership is documented in [`backend/docs/auth-data-ownership.md`](docs/auth-data-ownership.md).
- Current catalog/title schema behavior is documented in [`backend/docs/title-catalog-schema.md`](docs/title-catalog-schema.md).
- Local developer enrollment is request-based. Players submit enrollment requests, moderators approve or reject them, and approval uses the backend client's Keycloak service account to grant the `developer` realm role.

Local Keycloak bootstrap defaults:

- Keycloak admin console: [`https://localhost:8443/admin/`](https://localhost:8443/admin/)
- Keycloak bootstrap admin: `admin` / `admin`
- Seeded realm user for login testing: `local-admin` / `ChangeMe!123`
- Local verification email inbox: [`https://localhost:8025`](https://localhost:8025)

To verify the browser login flow locally, open:

```bash
https://localhost:7085/identity/auth/login
```

Keycloak will host the login/registration UI and redirect back to:

```bash
https://localhost:7085/identity/auth/callback
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
