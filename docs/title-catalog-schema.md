# Title Catalog Schema (Waves 3 and 4)

This document records the maintained title/catalog model that is currently implemented in the backend through Waves 3 and 4.

## Table of Contents

- [Purpose](#purpose)
- [Current Scope](#current-scope)
- [Public Routing And Discoverability](#public-routing-and-discoverability)
- [Relational Tables](#relational-tables)
- [Lifecycle And Visibility Model](#lifecycle-and-visibility-model)
- [Metadata Revision Behavior](#metadata-revision-behavior)
- [Media Asset Model](#media-asset-model)
- [Release And Artifact Model](#release-and-artifact-model)
- [Public API Shape](#public-api-shape)
- [Age Ratings And Derived Display Fields](#age-ratings-and-derived-display-fields)
- [Schema References](#schema-references)
- [Out Of Scope For The Current Wave Boundary](#out-of-scope-for-the-current-wave-boundary)

## Purpose

Use this document for the current title/catalog schema and behavioral rules around titles, versioned metadata, media, releases, and artifact metadata.

This is a maintained design/reference doc, but it is not a second exhaustive schema source of truth. Exact column names, constraints, comments, and foreign keys still live in EF Core configurations and migrations.

## Current Scope

The current implemented scope includes:

- public catalog browsing via `/catalog`
- storefront-style public title detail via `/catalog/{organizationSlug}/{titleSlug}`
- authenticated title management scoped to organizations
- versioned metadata snapshots for player-facing catalog copy
- draft/testing/published/archived lifecycle state
- private/unlisted/listed visibility state
- ESRB/PEGI-style age rating authority and value fields
- fixed media slots for card, hero, and logo assets
- semver release records bound to metadata revisions
- APK artifact metadata records for published releases

## Public Routing And Discoverability

Public title routing is organization-scoped to prevent ambiguity across different developers:

- `/catalog/{organizationSlug}/{titleSlug}`

Discoverability is intentionally separate from lifecycle:

- `listed` titles appear in public catalog browse results when the title is in `testing` or `published`
- `unlisted` titles do not appear in public browse results, but public detail remains reachable by direct route key
- `private` titles are not publicly reachable

## Relational Tables

The current title/catalog surface uses these PostgreSQL tables:

- `titles`
- `title_metadata_versions`
- `title_media_assets`
- `title_releases`
- `release_artifacts`

High-level ownership split:

- `titles` stores stable title identity, owning organization, lifecycle state, visibility, and the pointer to the currently active metadata revision
- `title_metadata_versions` stores player-facing metadata snapshots with per-title revision numbers
- `title_media_assets` stores fixed Board-style media slots per title
- `title_releases` stores semver release history bound to a title and one metadata revision
- `release_artifacts` stores installable artifact metadata for a release

Important integrity rules:

- title slugs are unique only within an organization
- metadata revision numbers are unique only within a title
- `titles.current_metadata_version_id` is constrained so it can only reference metadata that belongs to the same title
- media roles are unique only within a title
- release versions are unique only within a title
- `title_releases.metadata_version_id` is constrained so a release can only reference metadata that belongs to the same title
- `titles.current_release_id` is constrained so it can only reference a release that belongs to the same title
- artifact identity is unique only within a release by `(package_name, version_code)`

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

Metadata revisions continue to use the agreed "mutable draft, frozen after draft" model.

Current behavior:

- creating a title requires an initial metadata revision
- the first metadata revision is revision `1`
- while a title is still `draft` and its current revision is not frozen, metadata updates happen in place
- when a title leaves `draft`, the active metadata revision is frozen
- once a title is no longer `draft`, further metadata edits create a new revision and repoint `current_metadata_version_id`
- developers can reactivate an older revision, which supports rollback
- activating a revision for a non-draft title freezes that revision if it was not already frozen

This preserves low churn during drafting while still keeping stable metadata history once a title becomes public-facing.

## Media Asset Model

Wave 4 adds fixed-slot media rows for Board-style catalog presentation.

Current behavior:

- each title can have at most one `card`, one `hero`, and one `logo` asset
- media assets are title-scoped rather than metadata-revision-scoped
- each media row stores an external `source_url`, optional `alt_text`, optional `mime_type`, and optional width/height pair
- width and height must either both be absent or both be positive
- public catalog browse currently surfaces the card image URL when present

## Release And Artifact Model

Wave 4 adds semver releases and installable artifact metadata.

Current release behavior:

- each release belongs to exactly one title
- each release binds to exactly one metadata revision so shipped binaries always point at a known catalog snapshot
- release `status` currently allows `draft`, `published`, and `withdrawn`
- only `published` releases can be activated as `titles.current_release_id`
- developers can activate an older published release to support rollback
- withdrawing the active release clears `titles.current_release_id`
- draft releases remain mutable; published or withdrawn releases are treated as immutable history by the API

Current artifact behavior:

- artifacts currently allow `artifact_kind = apk` only
- artifact rows store package identity plus optional integrity metadata
- artifact rows do not yet expose or persist install/download URLs
- publishing a release requires at least one artifact

## Public API Shape

Wave 4 extends public catalog responses without exposing install delivery.

Current behavior:

- `GET /catalog` can include `cardImageUrl` in title summaries
- `GET /catalog/{organizationSlug}/{titleSlug}` includes `mediaAssets` and `currentRelease`
- `currentRelease` is a summary view only; artifact internals remain developer-only
- developer endpoints manage media, releases, publish/activate/withdraw transitions, and artifact metadata under `/developer/titles/{titleId}/...`

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
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Entities/TitleMediaAsset.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Entities/TitleMediaAsset.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Entities/TitleRelease.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Entities/TitleRelease.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Entities/ReleaseArtifact.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Entities/ReleaseArtifact.cs)
- EF configurations:
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleConfiguration.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleConfiguration.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleMetadataVersionConfiguration.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleMetadataVersionConfiguration.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleMediaAssetConfiguration.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleMediaAssetConfiguration.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleReleaseConfiguration.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/TitleReleaseConfiguration.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/ReleaseArtifactConfiguration.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Configurations/ReleaseArtifactConfiguration.cs)
- migrations:
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Migrations/20260301225254_Wave3TitlesMetadata.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Migrations/20260301225254_Wave3TitlesMetadata.cs)
  - [`backend/src/Board.ThirdPartyLibrary.Api/Persistence/Migrations/20260302010127_Wave4MediaReleasesArtifacts.cs`](../src/Board.ThirdPartyLibrary.Api/Persistence/Migrations/20260302010127_Wave4MediaReleasesArtifacts.cs)
- API contract:
  - [`api/postman/specs/board-third-party-library-api.v1.openapi.yaml`](../../api/postman/specs/board-third-party-library-api.v1.openapi.yaml)

## Out Of Scope For The Current Wave Boundary

The following remain out of scope until Wave 5 or later:

- external integration bindings and provider-specific host configuration
- checkout, orders, entitlements, and monetization flows
- Board-device install orchestration and artifact delivery URLs

Semver belongs on `title_releases`, not on `title_metadata_versions`. Metadata revisions capture catalog copy, not release identity. Artifact delivery remains deferred until the Board-device workflow is defined.
