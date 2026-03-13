This folder holds the minimal shared-workspace files needed for repo-local backend CI.

Why it exists:

- the standalone backend validation job must run without cloning the root repository
- the Workers package currently references the shared migration contract and root `tsconfig.base.json`
- the integration validation job still checks the composed root workspace separately

How it is used:

- the standalone workflow copies `packages/migration-contract` and `tsconfig.base.json` to the sibling paths the backend package expects
- the integration workflow still clones the real root repository and can be pointed at a coordinated branch via:
  - workflow-dispatch input `root_ref`
  - PR label `root-ref:<branch>`

Maintenance note:

- keep these vendored files aligned with the root repository copies whenever the shared migration contract or base TypeScript config changes
