-- 017_users_cleanup.sql
-- Bereinigt users-Tabellen, die schon vor diesen Skripten existierten: 001 legt fehlende Spalten
-- mit Default an, lässt vorhandene Spalten aber unverändert. Dort können
--   * Usernames stehen, die gegen das Format aus 012 verstoßen – dann scheitert JEDES Update der Zeile
--     (auch nur der Bio) an users_username_format, weil Postgres "not valid"-Checks bei Änderungen prüft,
--   * NULL-Werte stehen, an denen das Laden/Speichern des Profils in der App scheiterte.

-- 1) Usernames zuerst: kleinschreiben, ungültige Zeichen → "_", max. 30 Zeichen, keine Duplikate
with cleaned as (
    select id, left(regexp_replace(lower(username), '[^a-z0-9._]', '_', 'g'), 30) as clean
    from public.users
    where username is not null and username !~ '^[a-z0-9._]{3,30}$'
), ranked as (
    select c.id, c.clean, row_number() over (partition by c.clean order by c.id) as rn
    from cleaned c
)
update public.users u
set username = r.clean
from ranked r
where u.id = r.id
  and r.rn = 1
  and length(r.clean) >= 3
  and not exists (select 1 from public.users o where o.id <> u.id and o.username = r.clean);

-- Was sich nicht reparieren lässt: kein Username → die App bittet um "Complete profile"
update public.users
set username = null
where username is not null and username !~ '^[a-z0-9._]{3,30}$';

-- 2) NULL → leere Texte bzw. 0 / false
update public.users
set display_name      = coalesce(display_name, ''),
    vorname           = coalesce(vorname, ''),
    nachname          = coalesce(nachname, ''),
    bio               = coalesce(bio, ''),
    profile_image_url = coalesce(profile_image_url, ''),
    followers_count   = coalesce(followers_count, 0),
    following_count   = coalesce(following_count, 0),
    posts_count       = coalesce(posts_count, 0),
    is_verified       = coalesce(is_verified, false),
    created_at        = coalesce(created_at, now())
where display_name is null or vorname is null or nachname is null or bio is null
   or profile_image_url is null or followers_count is null or following_count is null
   or posts_count is null or is_verified is null or created_at is null;

-- Bio auf das Limit aus 012 kürzen, sonst scheitern Updates an users_bio_length
update public.users set bio = left(bio, 160) where char_length(bio) > 160;

-- 3) Ab jetzt keine NULLs mehr (idempotent)
alter table public.users
    alter column display_name      set default '', alter column display_name      set not null,
    alter column vorname           set default '', alter column vorname           set not null,
    alter column nachname          set default '', alter column nachname          set not null,
    alter column bio               set default '', alter column bio               set not null,
    alter column profile_image_url set default '', alter column profile_image_url set not null,
    alter column followers_count   set default 0,  alter column followers_count   set not null,
    alter column following_count   set default 0,  alter column following_count   set not null,
    alter column posts_count       set default 0,  alter column posts_count       set not null,
    alter column is_verified       set default false, alter column is_verified    set not null,
    alter column created_at        set default now(), alter column created_at     set not null;

-- Die Checks aus 012 gelten nach der Bereinigung für alle Zeilen
alter table public.users validate constraint users_username_format;
alter table public.users validate constraint users_bio_length;
