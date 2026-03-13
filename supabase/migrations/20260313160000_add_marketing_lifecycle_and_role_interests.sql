alter table public.marketing_contacts
    add column if not exists lifecycle_status text not null default 'waitlisted'
        check (lifecycle_status in ('waitlisted', 'invited', 'converted'));

create table if not exists public.marketing_contact_role_interests (
    marketing_contact_id uuid not null references public.marketing_contacts(id) on delete cascade,
    role text not null check (role in ('player', 'developer')),
    created_at timestamptz not null default now(),
    primary key (marketing_contact_id, role)
);

create index if not exists idx_marketing_contact_role_interests_role
    on public.marketing_contact_role_interests(role);

alter table public.marketing_contact_role_interests enable row level security;
