# Title Catalog Schema (Wave 3)

This document records the maintained Wave 3 catalog model that is currently implemented in the backend.

## Table of Contents

- [Purpose](#purpose)
- [Current Scope](#current-scope)
- [Public Routing And Discoverability](#public-routing-and-discoverability)
- [Relational Tables](#relational-tables)
- [Lifecycle And Visibility Model](#lifecycle-and-visibility-model)
- [Metadata Revision Behavior](#metadata-revision-behavior)
- [Age Ratings And Derived Display Fields](#age-ratings-and-derived-display-fields)
- [Schema References](#schema-references)
- [Out Of Scope For Wave 3](#out-of-scope-for-wave-3)

## Purpose

Use this document for the current Wave 3 schema and behavioral rules around titles and versioned metadata.

This is a maintained design/reference doc, but it is not a second exhaustive schema source of truth. Exact column names, constraints, comments, and foreign keys still live in EF Core configurations and migrations.

## Current Scope

Wave 3 implements:

- public catalog browsing via `/catalog`
- storefront-style public title detail via `/catalog/{organizationSlug}/{titleSlug}`
- authenticated title management scoped to organizations
- versioned metadata snapshots for player-facing catalog copy
- draft/testing/published/archived lifecycle state
- private/unlisted/listed visibility state
- ESRB/PEGI-style age rating authority and value fields

## Public Routing And Discoverability

Public title routing is organization-scoped to prevent ambiguity across different developers:

- `/catalog/{organizationSlug}/{titleSlug}`

Discoverability is intentionally separate from lifecycle:

- `listed` titles appear in public catalog browse results when the title is in `testing` or `published`
- `unlisted` titles do not appear in public browse results, but public detail remains reachable by direct route key
- `private` titles are not publicly reachable

## Relational Tables

Wave 3 adds these PostgreSQL tables:

- `titles`
- `title_metadata_versions`

High-level ownership split:

- `titles` stores stable title identity, owning organization, lifecycle state, visibility, and the pointer to the currently active metadata revision
- `title_metadata_versions` stores player-facing metadata snapshots with per-title revision numbers

Important integrity rules:

- title slugs are unique only within an organization
- metadata revision numbers are unique only within a title
- `titles.current_metadata_version_id` is constrained so it can only reference metadata that belongs to the same title

## Lifecycle And Visibility Model

`titles.lifecycle_status` currently allows:

- `draft`
- `testing`
- `published`
- `archived`

`titles.visibility` currently allows:

- `private`
- `unlisted`
- `listed`

Current behavior:

- `draft` titles must be `private`
- `testing` and `published` titles can use any visibility value
- `archived` titles remain queryable to authorized developers but are excluded from public catalog behavior

## Metadata Revision Behavior

Wave 3 uses the agreed "mutable draft, frozen after draft" model.

Current behavior:

- creating a title requires an initial metadata revision
- the first metadata revision is revision `1`
- while a title is still `draft` and its current revision is not frozen, metadata updates happen in place
- when a title leaves `draft`, the active metadata revision is frozen
- once a title is no longer `draft`, further metadata edits create a new revision and repoint `current_metadata_version_id`
- developers can reactivate an older revision, which supports rollback
- activating a revision for a non-draft title freezes that revision if it was not already frozen

This preserves low churn during drafting while still keeping stable metadata history once a title becomes public-facing.

## Age Ratings And Derived Display Fields

Wave 3 stores structured rating data as:

- `age_rating_authority`
- `age_rating_value`
- `min_age_years`

This supports ESRB/PEGI-style labels without hard-coding a single regional system.

Wave 3 does not persist presentation-only display strings for:

- player counts
- age rating display

Instead, the API derives:

- `playerCountDisplay` from `minPlayers` and `maxPlayers`
- `ageDisplay` from `ageRatingAuthority` and `ageRatingValue`

## Schema References

Use these maintained implementation artifacts as the authoritative references:

- EF entities:
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Entities/Title.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Entities/Title.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Entities/TitleMetadataVersion.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Entities/TitleMetadataVersion.cs)
- EF configurations:
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleConfiguration.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleConfiguration.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleMetadataVersionConfiguration.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleMetadataVersionConfiguration.cs)
- migration:
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Migrations/20260301225254_Wave3TitlesMetadata.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Migrations/20260301225254_Wave3TitlesMetadata.cs)
- API contract:
  - [`api/postman/specs/board-third-party-library-api.v1.openapi.yaml`](../../api/postman/specs/board-third-party-library-api.v1.openapi.yaml)

## Out Of Scope For Wave 3

The following remain out of scope until Wave 4 or later:

- media assets
- releases and APK artifacts
- public semver enforcement

Semver belongs on `title_releases`, not on `title_metadata_versions`. Wave 3 metadata revisions capture catalog copy, not release identity.
