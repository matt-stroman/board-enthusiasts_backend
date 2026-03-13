create table if not exists public.marketing_contacts (
    id uuid primary key default gen_random_uuid(),
    email text not null,
    normalized_email text not null unique,
    first_name text null,
    status text not null check (status in ('subscribed', 'unsubscribed', 'bounced', 'suppressed', 'converted')),
    consented_at timestamptz not null,
    consent_text_version text not null,
    source text not null,
    utm_source text null,
    utm_medium text null,
    utm_campaign text null,
    utm_term text null,
    utm_content text null,
    brevo_contact_id text null,
    brevo_sync_state text not null default 'pending' check (brevo_sync_state in ('pending', 'synced', 'skipped', 'failed')),
    brevo_synced_at timestamptz null,
    brevo_last_error text null,
    converted_app_user_id uuid null references public.app_users(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists public.marketing_contact_environments (
    marketing_contact_id uuid not null references public.marketing_contacts(id) on delete cascade,
    environment text not null check (environment in ('production', 'staging')),
    status text not null check (status in ('none', 'invited', 'accepted', 'revoked')),
    invited_at timestamptz null,
    auth_user_id uuid null,
    converted_app_user_id uuid null references public.app_users(id) on delete set null,
    notes text null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (marketing_contact_id, environment)
);

create index if not exists idx_marketing_contacts_status on public.marketing_contacts(status);
create index if not exists idx_marketing_contacts_source on public.marketing_contacts(source);
create index if not exists idx_marketing_contacts_created_at on public.marketing_contacts(created_at desc);

alter table public.marketing_contacts enable row level security;
alter table public.marketing_contact_environments enable row level security;
