# Postman API Testing Assets (Legacy)

This folder contains legacy backend-local Postman assets kept for historical/local experimentation only.

It is not the maintained source of truth for the current API contract or contract test workflow.

## Maintained Source Of Truth

Use these maintained assets instead:

- OpenAPI spec: [`api/postman/specs/board-enthusiasts-api.v1.openapi.yaml`](../../api/postman/specs/board-enthusiasts-api.v1.openapi.yaml)
- Contract test collection: [`api/postman/collections/board-enthusiasts-api.contract-tests.postman_collection.json`](../../api/postman/collections/board-enthusiasts-api.contract-tests.postman_collection.json)
- Local environment template: [`api/postman/environments/board-enthusiasts_local.postman_environment.json`](../../api/postman/environments/board-enthusiasts_local.postman_environment.json)
- Workflow docs: [`backend/docs/postman-api-testing.md`](../docs/postman-api-testing.md) and [`api/README.md`](../../api/README.md)

## Legacy Contents

- [`collections/BoardThirdPartyLibrary.Api.postman_collection.json`](collections/BoardThirdPartyLibrary.Api.postman_collection.json)
- [`environments/local.postman_environment.json`](environments/local.postman_environment.json)

## Why This Folder Still Exists

- preserve earlier backend-only Postman work
- avoid breaking references while the maintained workflow points to the `api` submodule
- provide a place for temporary/local-only backend request experiments when needed

## Current Legacy Coverage

- `GET /`
- `GET /health/live`
- `GET /health/ready`

Do not update this folder when changing the maintained API contract. Update the `api/postman/` assets instead.
