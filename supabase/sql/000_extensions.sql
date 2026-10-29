-- 000_extensions.sql
-- Extensions und gemeinsame Hilfsfunktionen

create extension if not exists pgcrypto;
create extension if not exists pg_trgm;   -- unscharfe Suche nach Usernamen / Hashtags

-- Setzt updated_at automatisch bei jedem UPDATE
create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;
