# Email Templates

This directory contains HTML email templates for two different delivery paths in the Board Enthusiasts stack.

## Supabase Auth Templates

These templates match the standard Supabase auth email lifecycle and are intended to be uploaded to the Supabase project email settings when that flow is configured:

- `confirmation.html`
- `email-change.html`
- `invite.html`
- `magic-link.html`
- `reauthentication.html`
- `recovery.html`

These templates should continue to use the placeholder variables expected by Supabase auth.

## Board Enthusiasts Campaign and Transactional Templates

These templates are intended for BE-branded operational or marketing sends outside the default Supabase auth lifecycle:

- `beta-invite.html`
- `news-update.html`

These are designed to align with the current public site direction in `frontend/` and can be adapted for Brevo transactional templates or campaign sends.

## Current Template Variables

### `beta-invite.html`

- `{{ .ConfirmationURL }}`
- `{{ .SiteURL }}`
- `{{ if .Data.firstName }}...{{ end }}`

Recommended usage:

- early-access invitations
- staged rollout invitations
- “your account is ready” style onboarding sends

### `news-update.html`

- `{{ .SiteURL }}`
- `{{ .Data.headline }}`
- `{{ .Data.summary }}`
- `{{ .Data.primaryUrl }}`
- `{{ .Data.primaryLabel }}`
- `{{ .Data.detailOne }}`
- `{{ .Data.detailTwo }}`

Recommended usage:

- launch updates
- roadmap progress emails
- developer-resource announcements
- blog and feature-roundup emails

## Implementation Notes

- Keep unsubscribe handling in the sending platform for newsletter-style mail.
- Keep BE independence wording in public-facing sends.
- Avoid using `support@boardenthusiasts.com` for bulk mail; prefer `updates@boardenthusiasts.com` or `news@boardenthusiasts.com`.
- When template content changes, keep the visual system aligned with the current landing page rather than creating a separate email-only brand.
