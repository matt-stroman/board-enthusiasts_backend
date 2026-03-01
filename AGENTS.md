# Board Third Party Library Backend

## Coding Standard

- Implement new external API behavior contract-first and TDD-first: OpenAPI/Postman coverage first, failing backend tests second, production code last.
- Keep the maintained backend behavior aligned with the current implemented contract; do not leave future-only endpoints partially represented in docs/tests if persistence and implementation are deferred.
- Keycloak owns authentication lifecycle behavior and brokered SSO provider linkage. PostgreSQL should only own application data and local projections keyed to Keycloak subjects.
- Every existing and new web API endpoint must have thorough unit test(s) to cover the endpoint's typical and edge cases.
- If code is found that is not covered, write applicable unit tests to cover it.
- Work from a branch, commit the completed change set, push it, and open or update a PR.
- Wait for the relevant GitHub workflow runs, inspect failures, and push fixes until the branch is green.
- Merge to `main` only after the required checks pass.
- After the PR is merged, delete the merged branch locally and remotely, prune stale remote refs, and leave the repository on a clean `main` tracking `origin/main`.
