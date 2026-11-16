-- bootstrap.sql
-- Minimale Nachbildung der Supabase-Umgebung, die supabase/setup.sql voraussetzt.
-- Läuft als Superuser "postgres" vor setup.sql (siehe helpers/db.js).

-- Rollen wie bei Supabase (ohne Login; "set role" als Superuser geht trotzdem)
create role anon nologin noinherit;
create role authenticated nologin noinherit;
create role service_role nologin noinherit bypassrls;

-- Suchpfad wie im Supabase-SQL-Editor
alter database postgres set search_path to "$user", public, extensions;
set search_path to "$user", public, extensions;

-- Extensions: pgcrypto ist bei Supabase vorinstalliert (Schema extensions),
-- pg_trgm nicht – das legt 000_extensions.sql selbst an (landet dann in public).
create schema if not exists extensions;
grant usage on schema extensions to anon, authenticated, service_role;
create extension if not exists pgcrypto with schema extensions;

-- Rechte auf public wie bei Supabase: alles für anon/authenticated/service_role,
-- auch für künftig angelegte Objekte. Die revoke/grant-Anweisungen der Skripte
-- sind damit das, was tatsächlich zählt.
grant usage on schema public to anon, authenticated, service_role;
alter default privileges in schema public grant all on tables    to anon, authenticated, service_role;
alter default privileges in schema public grant all on functions to anon, authenticated, service_role;
alter default privileges in schema public grant all on sequences to anon, authenticated, service_role;

-- ---------------------------------------------------------------------
-- auth
-- ---------------------------------------------------------------------
create schema if not exists auth;
grant usage on schema auth to anon, authenticated, service_role;

create table auth.users (
    id                 uuid primary key default gen_random_uuid(),
    email              text unique,
    raw_user_meta_data jsonb not null default '{}'::jsonb,
    created_at         timestamptz not null default now(),
    updated_at         timestamptz not null default now()
);

create or replace function auth.uid()
returns uuid
language sql
stable
as $$
    select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid;
$$;

create or replace function auth.role()
returns text
language sql
stable
as $$
    select nullif(current_setting('request.jwt.claim.role', true), '')::text;
$$;

grant execute on function auth.uid(), auth.role() to anon, authenticated, service_role;

-- ---------------------------------------------------------------------
-- storage
-- ---------------------------------------------------------------------
create schema if not exists storage;
grant usage on schema storage to anon, authenticated, service_role;

create table storage.buckets (
    id                 text primary key,
    name               text not null unique,
    owner              uuid,
    public             boolean not null default false,
    file_size_limit    bigint,
    allowed_mime_types text[],
    created_at         timestamptz not null default now(),
    updated_at         timestamptz not null default now()
);

create table storage.objects (
    id         uuid primary key default gen_random_uuid(),
    bucket_id  text references storage.buckets (id),
    name       text,
    owner      uuid default auth.uid(),
    metadata   jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (bucket_id, name)
);

alter table storage.buckets enable row level security;
alter table storage.objects enable row level security;

grant all on storage.buckets, storage.objects to anon, authenticated, service_role;

-- Wie bei Supabase: alle Ordner des Pfads ohne den Dateinamen
create or replace function storage.foldername(name text)
returns text[]
language plpgsql
immutable
as $$
declare
    _parts text[];
begin
    select string_to_array(name, '/') into _parts;
    return _parts[1:array_length(_parts, 1) - 1];
end;
$$;

grant execute on function storage.foldername(text) to anon, authenticated, service_role;

-- ---------------------------------------------------------------------
-- Realtime
-- ---------------------------------------------------------------------
create publication supabase_realtime;
