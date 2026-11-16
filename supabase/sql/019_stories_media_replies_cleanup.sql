-- 019_stories_media_replies_cleanup.sql
-- Phase 4: Videos in Stories, Antworten auf Stories (als Direktnachricht),
-- automatisches Löschen abgelaufener Stories inkl. Dateien.

-- ---------------------------------------------------------------------
-- Videos in Stories
-- ---------------------------------------------------------------------

-- image_url enthält bei Videos die Video-URL (wie post_images bei Reels).
alter table public.stories
    add column if not exists media_type text not null default 'image';

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'stories_media_type_check') then
        alter table public.stories
            add constraint stories_media_type_check check (media_type in ('image', 'video'));
    end if;
end;
$$;

-- story_feed (015) um media_type erweitern (neue Spalten nur am Ende)
create or replace view public.story_feed
with (security_invoker = true)
as
select
    s.id,
    s.user_id,
    s.image_url,
    s.created_at,
    s.expires_at,
    u.username,
    u.display_name,
    u.vorname,
    u.nachname,
    u.profile_image_url,
    u.is_verified,
    exists (select 1 from public.story_views v where v.story_id = s.id and v.user_id = auth.uid()) as is_viewed,
    (select count(*) from public.story_views v where v.story_id = s.id)::integer as view_count,
    s.media_type
from public.stories s
join public.users u on u.id = s.user_id
where s.expires_at > now()
  and (
      s.user_id = auth.uid()
      or exists (select 1 from public.follows f where f.follower_id = auth.uid() and f.following_id = s.user_id)
  );

-- Bucket "stories": Bilder und kurze Videos, max. 50 MB
update storage.buckets
set file_size_limit = 52428800,
    allowed_mime_types = array['image/jpeg', 'image/png', 'image/webp', 'video/mp4', 'video/webm', 'video/quicktime']
where id = 'stories';

-- ---------------------------------------------------------------------
-- Antworten auf Stories
-- ---------------------------------------------------------------------

-- story_id wird beim Löschen der Story (auch durch das Aufräumen) geleert;
-- story_url bleibt als Hinweis, die Datei existiert dann aber nicht mehr.
alter table public.messages
    add column if not exists story_id  bigint references public.stories (id) on delete set null,
    add column if not exists story_url text;

-- Story-Bezug nur über reply_to_story, damit niemand fremde Bilder als "Story" ausgeben kann
drop policy if exists "messages_insert_member" on public.messages;
create policy "messages_insert_member" on public.messages
    for insert with check (
        sender_id = auth.uid()
        and public.is_conversation_member(conversation_id)
        and story_id is null
        and story_url is null
    );

-- Antwort auf eine sichtbare, fremde Story als Direktnachricht an den Autor.
-- Gibt die ID der Unterhaltung zurück.
create or replace function public.reply_to_story(p_story_id bigint, p_content text)
returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
    me        uuid := auth.uid();
    v_owner   uuid;
    v_url     text;
    v_conv_id bigint;
begin
    if me is null then
        raise exception 'not authenticated';
    end if;

    if char_length(coalesce(trim(p_content), '')) not between 1 and 2000 then
        raise exception 'replies need 1 to 2000 characters';
    end if;

    -- Gleiche Sichtbarkeit wie story_feed: aktiv und von einem Account, dem man folgt
    select s.user_id, s.image_url into v_owner, v_url
    from stories s
    where s.id = p_story_id
      and s.expires_at > now()
      and s.user_id <> me
      and exists (select 1 from follows f where f.follower_id = me and f.following_id = s.user_id);

    if v_owner is null then
        raise exception 'story not available';
    end if;

    v_conv_id := get_or_create_direct_conversation(v_owner);

    insert into messages (conversation_id, sender_id, content, story_id, story_url)
    values (v_conv_id, me, trim(p_content), p_story_id, v_url);

    return v_conv_id;
end;
$$;

revoke execute on function public.reply_to_story(bigint, text) from public, anon;
grant execute on function public.reply_to_story(bigint, text) to authenticated;

-- ---------------------------------------------------------------------
-- Abgelaufene Stories aufräumen
-- ---------------------------------------------------------------------
-- Dateien lassen sich nur über die Storage-API löschen (nicht per SQL), deshalb
-- erledigt das die Edge Function supabase/functions/cleanup-expired-stories.
-- pg_cron ruft sie stündlich über pg_net auf. Die Projekt-URL liegt im Vault:
--     select vault.create_secret('https://<project-ref>.supabase.co', 'project_url');
-- Ohne dieses Secret macht der Job nichts.

create or replace function public.invoke_story_cleanup()
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
    v_url text;
begin
    select decrypted_secret into v_url
    from vault.decrypted_secrets
    where name = 'project_url'
    limit 1;

    if coalesce(v_url, '') = '' then
        raise notice 'Vault-Secret project_url fehlt – Story-Aufräumen übersprungen';
        return;
    end if;

    perform net.http_post(
        url     := rtrim(v_url, '/') || '/functions/v1/cleanup-expired-stories',
        headers := jsonb_build_object('Content-Type', 'application/json'),
        body    := '{}'::jsonb
    );
end;
$$;

revoke execute on function public.invoke_story_cleanup() from public, anon, authenticated;

-- pg_cron / pg_net gibt es nur auf Supabase (bzw. mit den Extensions). Fehlen sie,
-- läuft das restliche Setup trotzdem durch.
do $$
begin
    create extension if not exists pg_cron with schema pg_catalog;
    create extension if not exists pg_net with schema extensions;
    -- Gleicher Name = vorhandener Job wird aktualisiert
    perform cron.schedule('cleanup-expired-stories', '17 * * * *', 'select public.invoke_story_cleanup()');
exception
    when others then
        raise notice 'pg_cron/pg_net nicht verfügbar (%), Story-Aufräumen nicht geplant', sqlerrm;
end;
$$;
