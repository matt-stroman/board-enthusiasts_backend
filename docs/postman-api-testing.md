# Postman API Testing (Backend)

This document explains how the backend Postman testing setup is organized and how to use it as a versioned API testing ground.

## Table of Contents

- [Why Postman works well here](#why-postman-works-well-here)
- [What is versioned in this repo](#what-is-versioned-in-this-repo)
- [Folder layout](#folder-layout)
- [How to use in Postman](#how-to-use-in-postman)
- [How to run via CLI (Newman)](#how-to-run-via-cli-newman)
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

## What is versioned in this repo

Backend-specific Postman assets are kept in the backend submodule:

- Collection: [`backend/postman/collections/BoardThirdPartyLibrary.Api.postman_collection.json`](../postman/collections/BoardThirdPartyLibrary.Api.postman_collection.json)
- Local environment: [`backend/postman/environments/local.postman_environment.json`](../postman/environments/local.postman_environment.json)
- Postman asset readme: [`backend/postman/README.md`](../postman/README.md)
- Optional CLI wrapper: [`backend/scripts/run-postman.ps1`](../scripts/run-postman.ps1)

## Folder layout

```text
backend/
  postman/
    collections/
    environments/
  scripts/
  docs/
  planning/
```

Separation rule applied:

- backend-specific API testing assets live in the backend submodule
- root repo remains focused on cross-project orchestration

## How to use in Postman

1. Start the backend locally:
   - from repo root: `python ./scripts/dev.py up`
   - see CLI docs for alternatives/options: [`docs/developer-cli.md`](../../docs/developer-cli.md)
2. Open Postman.
3. Import the collection file.
4. Import the local environment file.
5. Select the local environment.
6. Run individual requests or the full collection.

The collection includes request-level tests for the current endpoints and uses environment variables to keep expectations data-driven.

## How to run via CLI (Newman)

If you have Node.js installed, you can run the collection from the terminal using `npx`:

```powershell
pwsh ./backend/scripts/run-postman.ps1
```

You can also run a single folder (when the collection grows):

```powershell
pwsh ./backend/scripts/run-postman.ps1 -Folder "Health"
```

If `npx` is not available, install Node.js first (or run the collection in the Postman UI).

## How to evolve this as endpoints are added

When you add new endpoints:

1. Add a request to the collection.
2. Add response assertions in the request’s Postman test script.
3. Add/adjust environment variables instead of hardcoding environment-specific values.
4. Update this doc or the Postman README only when the workflow changes.

Recommended patterns:

- Keep one collection per backend service/API boundary
- Use folders by domain (`Catalog`, `Developer Integrations`, `Payments`, etc.)
- Add both happy-path and error-path requests when endpoint behavior becomes more complex
- Keep shared secrets out of committed environments; use private local environment files instead
