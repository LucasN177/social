-- 013_posts_api.sql
-- Lese- und Schreib-API für Phase 2 (Posts, Kommentare).

-- Feed um Vor-/Nachname erweitern (für Initialen-Avatare).
-- "create or replace view" erlaubt neue Spalten nur am Ende.
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
    u.nachname
from public.posts p
join public.users u on u.id = p.user_id;

-- Kommentare inkl. Autor
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
    u.is_verified
from public.comments c
join public.users u on u.id = c.user_id;

-- Post + Bilder in einer Transaktion anlegen. security invoker: die RLS-Policies
-- aus 002_posts.sql gelten weiterhin (nur eigene Posts / Bilder).
create or replace function public.create_post(
    p_content    text,
    p_location   text default '',
    p_tags       text[] default '{}',
    p_image_urls text[] default '{}'
)
returns bigint
language plpgsql
security invoker
set search_path = public
as $$
declare
    new_id bigint;
begin
    if auth.uid() is null then
        raise exception 'not authenticated';
    end if;

    if coalesce(trim(p_content), '') = '' and coalesce(array_length(p_image_urls, 1), 0) = 0 then
        raise exception 'post needs text or at least one image';
    end if;

    if coalesce(array_length(p_image_urls, 1), 0) > 10 then
        raise exception 'at most 10 images per post';
    end if;

    insert into posts (user_id, content, location, tags, media_type)
    values (
        auth.uid(),
        coalesce(p_content, ''),
        coalesce(p_location, ''),
        coalesce(p_tags, '{}'),
        case when coalesce(array_length(p_image_urls, 1), 0) > 0 then 'image' else 'text' end
    )
    returning id into new_id;

    insert into post_images (post_id, url, position)
    select new_id, url, (ord - 1)::smallint
    from unnest(p_image_urls) with ordinality as t(url, ord);

    return new_id;
end;
$$;

-- Länge des Beitragstexts begrenzen (wie in der App)
do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'posts_content_length') then
        alter table public.posts
            add constraint posts_content_length check (char_length(content) <= 2200) not valid;
    end if;
end;
$$;
