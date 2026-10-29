-- 001_users.sql
-- Öffentliches Profil je Auth-User. Wird per Trigger beim Registrieren angelegt,
-- die App ergänzt danach Username / Vorname / Nachname (AuthRepository.InsertUserMetadata).

create table if not exists public.users (
    id                uuid primary key references auth.users (id) on delete cascade,
    username          text unique,
    display_name      text not null default '',
    vorname           text not null default '',
    nachname          text not null default '',
    bio               text not null default '',
    profile_image_url text not null default '',
    followers_count   integer not null default 0,
    following_count   integer not null default 0,
    posts_count       integer not null default 0,
    is_verified       boolean not null default false,
    created_at        timestamptz not null default now(),
    updated_at        timestamptz not null default now()
);

-- Falls die Tabelle schon existiert: fehlende Spalten nachziehen
alter table public.users add column if not exists display_name      text not null default '';
alter table public.users add column if not exists vorname           text not null default '';
alter table public.users add column if not exists nachname          text not null default '';
alter table public.users add column if not exists bio               text not null default '';
alter table public.users add column if not exists profile_image_url text not null default '';
alter table public.users add column if not exists followers_count   integer not null default 0;
alter table public.users add column if not exists following_count   integer not null default 0;
alter table public.users add column if not exists posts_count       integer not null default 0;
alter table public.users add column if not exists is_verified       boolean not null default false;
alter table public.users add column if not exists updated_at        timestamptz not null default now();

create index if not exists users_username_trgm_idx
    on public.users using gin (username gin_trgm_ops);

drop trigger if exists users_set_updated_at on public.users;
create trigger users_set_updated_at
    before update on public.users
    for each row execute function public.set_updated_at();

-- Profilzeile automatisch anlegen, sobald sich jemand registriert
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    -- Die App übergibt username / vorname / nachname / display_name beim SignUp als User-Metadaten
    insert into public.users (id, username, display_name, vorname, nachname)
    values (
        new.id,
        nullif(new.raw_user_meta_data ->> 'username', ''),
        coalesce(new.raw_user_meta_data ->> 'display_name', ''),
        coalesce(new.raw_user_meta_data ->> 'vorname', ''),
        coalesce(new.raw_user_meta_data ->> 'nachname', '')
    )
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_user();

-- Bereits registrierte User ohne Profilzeile nachtragen
insert into public.users (id)
select a.id from auth.users a
on conflict (id) do nothing;

-- RLS
alter table public.users enable row level security;

drop policy if exists "users_select_all" on public.users;
create policy "users_select_all" on public.users
    for select using (true);

drop policy if exists "users_update_own" on public.users;
create policy "users_update_own" on public.users
    for update using (auth.uid() = id) with check (auth.uid() = id);
