# Storage Buckets

## Summary

Board Enthusiasts uses a typed Supabase Storage bucket model instead of a single shared media bucket.

Current bucket inventory:

- `avatars`
- `card-images`
- `hero-images`
- `logo-images`

All four buckets are currently provisioned as public buckets. The current frontend surfaces render these images directly in public SPA views, so signed URLs would add complexity without matching the current product shape.

## Bucket Inventory

### `avatars`

Use:

- reserved for compact square avatar imagery
- intended for user avatars and future studio avatars

Policy:

- max upload size: `256 KB`
- allowed MIME types:
  - `image/webp`
  - `image/jpeg`
  - `image/png`

Recommended authoring target:

- aspect ratio: `1:1`
- preferred size: `256x256`
- do not exceed `512x512`

Current implementation note:

- the existing user avatar flow still stores avatar data URLs directly in application data instead of uploading to Supabase Storage
- the bucket exists now so future avatar surfaces can converge on the typed storage model

### `card-images`

Use:

- title browse/discovery card artwork

Policy:

- max upload size: `1536 KB`
- allowed MIME types:
  - `image/webp`
  - `image/jpeg`
  - `image/png`

Recommended authoring target:

- aspect ratio: `3:4`
- preferred size: `900x1200`
- avoid exceeding `1200x1600`

### `hero-images`

Use:

- title hero artwork
- studio banner artwork

Policy:

- max upload size: `3 MB`
- allowed MIME types:
  - `image/webp`
  - `image/jpeg`
  - `image/png`
  - `image/svg+xml`

Recommended authoring target:

- wide cover/banner imagery
- preferred size: roughly `1800x600`
- avoid exceeding `2400x800` for raster uploads

Notes:

- `image/svg+xml` is intentionally allowed here because the current seeded studio banner assets are vector-based

### `logo-images`

Use:

- studio logos
- title logos / wordmarks

Policy:

- max upload size: `256 KB`
- allowed MIME types:
  - `image/webp`
  - `image/png`
  - `image/svg+xml`

Recommended authoring target:

- transparency-friendly artwork
- prefer SVG when a true vector logo exists
- for raster uploads, target roughly `1024x512` or smaller

## Why This Split

This split maps to the actual UI surfaces in the current application:

- avatars are compact and square
- card images are portrait-oriented discovery art
- hero images are large wide background surfaces
- logo images favor sharp edges, transparency, and vector support

This lets us enforce different MIME and size rules per category while still keeping the bucket model small enough to manage operationally.

## Supabase Free Plan Considerations

The current Supabase free plan includes limited storage and egress, so the application should not rely on oversized original files.

Key implication:

- uploaded media should already be close to display-ready
- do not rely on runtime image transformation on the free plan

The current bucket limits are intentionally tighter than the old `25 MB` generic cap, but still loose enough to admit the existing seeded demo catalog.

## Provisioning Strategy

Current provisioning path:

- bucket creation/update is handled by [`migration-seed.ts`](../scripts/migration-seed.ts)
- the root developer CLI passes the typed bucket names into the backend seed script

Current environment variables:

- `SUPABASE_AVATARS_BUCKET`
- `SUPABASE_CARD_IMAGES_BUCKET`
- `SUPABASE_HERO_IMAGES_BUCKET`
- `SUPABASE_LOGO_IMAGES_BUCKET`

This is acceptable for the current wave because:

- it is automated
- it keeps local and hosted configuration aligned
- it avoids manual dashboard bucket creation for the maintained stack

## Follow-ups

- optimize oversized seeded PNG artwork so bucket caps can be tightened further without breaking local/demo seeding
- converge the existing user avatar flow onto Supabase Storage instead of storing avatar data URLs in application rows
- revisit whether any future media classes should move to private buckets with signed delivery
