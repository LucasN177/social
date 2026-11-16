-- 018_post_edit_comment_likes.sql
-- Phase 2: Beiträge bearbeiten, Kommentar-Likes.

-- Zeitpunkt der letzten inhaltlichen Bearbeitung. updated_at taugt dafür nicht,
-- weil es auch bei jeder Zähleränderung (Likes, Kommentare) gesetzt wird.
alter table public.posts add column if not exists edited_at timestamptz;

-- Clients dürfen an eigenen Posts nur den Inhalt ändern, nicht die Zähler
-- (die Zähler-Trigger aus 008 sind security definer und davon nicht betroffen).
revoke update on public.posts from anon, authenticated;
grant update (content, location, tags, edited_at) on public.posts to authenticated;

-- Text, Ort und Hashtags eines eigenen Posts ändern. Bilder/Videos bleiben unverändert.
-- security invoker: die Policy posts_update_own (002) gilt weiterhin.
create or replace function public.update_post(
    p_post_id  bigint,
    p_content  text,
    p_location text default '',
    p_tags     text[] default '{}'
)
returns void
language plpgsql
security invoker
set search_path = public
as $$
declare
    v_media_type text;
begin
    if auth.uid() is null then
        raise exception 'not authenticated';
    end if;

    select media_type into v_media_type
    from posts
    where id = p_post_id and user_id = auth.uid();

    if v_media_type is null then
        raise exception 'post not found';
    end if;

    if v_media_type = 'text' and coalesce(trim(p_content), '') = '' then
        raise exception 'text posts need text';
    end if;

    update posts
    set content   = coalesce(p_content, ''),
        location  = coalesce(p_location, ''),
        tags      = coalesce(p_tags, '{}'),
        edited_at = now()
    where id = p_post_id and user_id = auth.uid();
end;
$$;

-- Feed um edited_at erweitern (neue Spalten nur am Ende, siehe README).
create or replace view public.feed_posts
with (security_invoker = true)
as
select
    p.id,
    p.user_id,
    p.content,
    p.location,
    p.tags,
    p.media_type,
    p.likes_count,
    p.comments_count,
    p.shares_count,
    p.created_at,
    u.username,
    u.display_name,
    u.profile_image_url,
    u.is_verified,
    coalesce(
        (select array_agg(pi.url order by pi.position) from public.post_images pi where pi.post_id = p.id),
        '{}'
    ) as image_urls,
    exists (select 1 from public.likes l     where l.post_id = p.id and l.user_id = auth.uid()) as is_liked,
    exists (select 1 from public.bookmarks b where b.post_id = p.id and b.user_id = auth.uid()) as is_bookmarked,
    u.vorname,
    u.nachname,
    p.edited_at
from public.posts p
join public.users u on u.id = p.user_id;

-- home_feed (014) übernimmt "f.*" nur beim Anlegen – neu erzeugen, damit edited_at dazukommt.
create or replace view public.home_feed
with (security_invoker = true)
as
select f.*
from public.feed_posts f
where f.user_id = auth.uid()
   or exists (
       select 1 from public.follows fo
       where fo.follower_id = auth.uid() and fo.following_id = f.user_id
   );

-- Kommentare um "selbst geliked" erweitern
create or replace view public.comment_details
with (security_invoker = true)
as
select
    c.id,
    c.post_id,
    c.user_id,
    c.content,
    c.likes_count,
    c.created_at,
    u.username,
    u.display_name,
    u.vorname,
    u.nachname,
    u.profile_image_url,
    u.is_verified,
    exists (select 1 from public.comment_likes cl where cl.comment_id = c.id and cl.user_id = auth.uid()) as is_liked
from public.comments c
join public.users u on u.id = c.user_id;
