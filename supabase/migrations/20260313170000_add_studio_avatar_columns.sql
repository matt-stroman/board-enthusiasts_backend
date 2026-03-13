alter table public.studios
  add column if not exists avatar_url text null,
  add column if not exists avatar_storage_path text null;
