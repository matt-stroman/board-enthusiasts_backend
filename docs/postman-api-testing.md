# Postman API Testing (Backend)

This document explains how backend developers should work with the maintained API contract tests.

## Table of Contents

- [Why Postman works well here](#why-postman-works-well-here)
- [Where the Maintained Assets Live](#where-the-maintained-assets-live)
- [Folder layout](#folder-layout)
- [How to use in Postman](#how-to-use-in-postman)
- [How to run via CLI](#how-to-run-via-cli)
- [How to evolve this as endpoints are added](#how-to-evolve-this-as-endpoints-are-added)

## Why Postman works well here

Yes, this is absolutely possible and a common pattern.

Postman collections and environments are JSON files, which means they can be:

- stored in source control
- reviewed in pull requests
- updated alongside endpoint changes
- executed manually in the Postman UI
- executed from the CLI (via Newman) for automation/CI later

This fits the project’s API-first direction well because the requests and assertions live next to the backend code and evolve with the endpoint contracts.

## Where the Maintained Assets Live

The maintained contract assets for current API behavior live in the `api` submodule:

- OpenAPI spec: [`api/postman/specs/board-enthusiasts-api.v1.openapi.yaml`](../../api/postman/specs/board-enthusiasts-api.v1.openapi.yaml)
- Contract test collection: [`api/postman/collections/board-enthusiasts-api.contract-tests.postman_collection.json`](../../api/postman/collections/board-enthusiasts-api.contract-tests.postman_collection.json)
- Local environment template: [`api/postman/environments/board-enthusiasts_local.postman_environment.json`](../../api/postman/environments/board-enthusiasts_local.postman_environment.json)
- API workflow guide: [`api/README.md`](../../api/README.md)

The backend-local [`backend/postman/`](../postman/) folder is retained only as legacy health-check experimentation and is not the maintained source of truth for the current contract.

## Folder layout

```text
backend/
  postman/          # legacy/local-only backend Postman assets
  scripts/
  docs/
  planning/
api/
  postman/
```

Separation rule applied:

- maintained API contract assets live in the `api` submodule
- backend docs point developers at the shared contract workflow
- root repo remains focused on cross-project orchestration

## How to use in Postman

1. Start the backend locally:
   - from repo root: `python ./scripts/dev.py up`
   - see CLI docs for alternatives/options: [`docs/developer-cli.md`](../../docs/developer-cli.md)
2. Open Postman.
3. Import the contract collection from `api/postman/collections/`.
4. Import the local environment template from `api/postman/environments/`.
5. Select the local environment.
6. Run individual requests or the full collection.

Important for live local runs:

- the committed local environment uses placeholders for `accessToken`, `moderatorAccessToken`, `developerSubject`, `studioId`, `studioSlug`, `titleId`, and `titleSlug`
- authenticated success-path requests are skipped until those placeholders are replaced with real local values
- Wave 4 and Wave 5 developer requests reuse the same `titleId`, `studioId`, and auth placeholders; no extra environment variables are required for media, release, artifact, supported-publisher, connection, or acquisition-binding requests
- this is expected and prevents false failures when developers only want public or unauthenticated smoke coverage

## How to run via CLI

Use the root CLI from the repository root:

```powershell
python ./scripts/dev.py api-test
```

If the backend is not already running, start it for the duration of the test run:

```powershell
python ./scripts/dev.py api-test --start-backend --skip-lint
```

Run mock-mode contract tests against a Postman mock:

```powershell
python ./scripts/dev.py api-test --base-url https://example.mock.pstmn.io --contract-execution-mode mock
```

## How to evolve this as endpoints are added

When you add new endpoints:

1. Add or update the OpenAPI contract first.
2. Add or update the request in the maintained contract test collection.
3. Add response assertions in the request’s Postman test script.
4. Add or adjust environment variables instead of hardcoding environment-specific values.
5. Update this doc or the API README only when the workflow changes.

Recommended patterns:

- Keep one maintained contract collection for the current API boundary
- Use folders by domain (`Catalog`, `Studios`, `Identity`, etc.)
- Add both happy-path and error-path coverage when endpoint behavior becomes more complex
- Prefer portable validation/conflict examples for Wave 4 and Wave 5 developer endpoints so the collection still runs cleanly with placeholder local environment values
- Keep shared secrets out of committed environments; use private local environment files instead


