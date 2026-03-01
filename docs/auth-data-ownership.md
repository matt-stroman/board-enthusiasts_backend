# Auth And Data Ownership

This document defines the current ownership boundary between Keycloak and the application database.

## Table of Contents

- [Purpose](#purpose)
- [Current State](#current-state)
- [What Keycloak Owns](#what-keycloak-owns)
- [What PostgreSQL Owns](#what-postgresql-owns)
- [Schema Implications](#schema-implications)
- [Local Development Notes](#local-development-notes)

## Purpose

Authentication is now delegated to Keycloak. That changes what the backend should persist in PostgreSQL and what it should treat as externally managed identity state.

Use this document when deciding whether new identity-related data belongs in:

- the Keycloak realm configuration, or
- the backend PostgreSQL schema.

## Current State

Current implemented foundation behavior:

- Keycloak hosts login and self-registration.
- Keycloak owns credentials, email verification, external identity-provider linkage, session/token lifecycle, and platform role assignment.
- The backend validates Keycloak-issued bearer tokens and reads platform roles from JWT claims.
- Keycloak is also the intended broker for future Google, Facebook, Steam, Epic Games, and similar SSO providers.
- PostgreSQL is not currently the source of truth for authentication data.

The backend does not yet have EF Core migrations or an implemented relational domain schema. This document defines the intended boundary before that schema work begins.

## What Keycloak Owns

Keycloak is the source of truth for:

- usernames and login identifiers
- passwords and credential policies
- email verification and password reset flows
- linked external identity providers
- platform roles such as `player`, `developer`, `admin`, and `moderator`
- session and token issuance/revocation state

These concerns should not be duplicated in PostgreSQL as primary auth tables.

## What PostgreSQL Owns

PostgreSQL remains the source of truth for application-owned domain data, including:

- organizations and memberships
- titles, metadata, media assets, releases, and artifacts
- provider integration configuration
- payment, entitlement, and install-delivery data when those areas are implemented
- optional Board profile linkage/cache owned by this application
- an application user projection, once persistence is introduced, for linking domain records to a Keycloak subject

Board profile persistence is planned application data, but it is not part of the current implemented API surface yet.

## Schema Implications

For the application database, the current direction is:

- do not introduce `user_password_credentials`
- do not introduce `user_email_addresses` as an auth-management model
- do not introduce `user_external_identities` for social/OIDC provider ownership
- do not make PostgreSQL the source of truth for platform role assignment

When the application needs a persisted user record, model it as an application identity projection linked to Keycloak. The expected minimum shape is:

- `users`
  - stable application user ID (`uuid`)
  - immutable `keycloak_subject` or equivalent unique external subject key
  - optional cached display fields when needed for local query/display convenience
- `user_board_profiles`
  - optional Board linkage/cache associated with the local `users` row

Important:

- cached Keycloak fields in PostgreSQL should be treated as non-authoritative snapshots
- platform authorization should continue to use Keycloak role claims unless a future local projection is justified by query/reporting needs

## Local Development Notes

Local identity seed data is currently provided by Keycloak realm import, not PostgreSQL seed scripts:

- Keycloak realm import file: [`backend/keycloak/import/board-third-party-library-realm.json`](../keycloak/import/board-third-party-library-realm.json)
- Local login test user: `local-admin` / `ChangeMe!123`

If new platform roles are introduced, update both:

- the Keycloak realm import
- the backend role catalog exposed by the API
