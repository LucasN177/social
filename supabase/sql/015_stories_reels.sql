-- 015_stories_reels.sql
-- Phase 4: Stories (24 h) und Reels (Video-Posts).

-- ---------------------------------------------------------------------
-- Stories
-- ---------------------------------------------------------------------

-- Aktive Stories von mir + gefolgten Accounts, inkl. Autor und Gesehen-Status.
-- view_count: Dank RLS auf story_views (006) sieht nur der Story-Besitzer alle Views.
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
    (select count(*) from public.story_views v where v.story_id = s.id)::integer as view_count
from public.stories s
join public.users u on u.id = s.user_id
where s.expires_at > now()
  and (
      s.user_id = auth.uid()
      or exists (select 1 from public.follows f where f.follower_id = auth.uid() and f.following_id = s.user_id)
  );

-- Story als gesehen markieren (mehrfaches Ansehen zählt einmal)
create or replace function public.mark_story_viewed(p_story_id bigint)
returns void
language sql
security invoker
set search_path = public
as $$
    insert into story_views (story_id, user_id)
    values (p_story_id, auth.uid())
    on conflict (story_id, user_id) do nothing;
$$;

-- Wer hat eine Story gesehen? RLS (006) beschränkt das auf den Besitzer.
create or replace function public.story_viewers(p_story_id bigint)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from story_views v
    join users u on u.id = v.user_id
    where v.story_id = p_story_id
    order by v.viewed_at desc;
$$;

create index if not exists stories_expires_idx on public.stories (expires_at);

-- ---------------------------------------------------------------------
-- Reels (Posts mit media_type = 'video'; die Video-URL liegt in post_images)
-- ---------------------------------------------------------------------

create or replace function public.create_reel(
    p_content   text,
    p_tags      text[] default '{}',
    p_video_url text default null
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

    if coalesce(trim(p_video_url), '') = '' then
        raise exception 'reel needs a video';
    end if;

    insert into posts (user_id, content, location, tags, media_type)
    values (auth.uid(), coalesce(p_content, ''), '', coalesce(p_tags, '{}'), 'video')
    returning id into new_id;

    insert into post_images (post_id, url, position)
    values (new_id, p_video_url, 0);

    return new_id;
end;
$$;

create index if not exists posts_media_type_idx on public.posts (media_type, created_at desc);

-- ---------------------------------------------------------------------
-- Storage: Bucket für Reels + Limits für Stories
-- ---------------------------------------------------------------------

insert into storage.buckets (id, name, public)
values ('reels', 'reels', true)
on conflict (id) do nothing;

-- Max. 50 MB und nur gängige Videoformate (Supabase prüft das beim Upload)
update storage.buckets
set file_size_limit = 52428800,
    allowed_mime_types = array['video/mp4', 'video/webm', 'video/quicktime']
where id = 'reels';

update storage.buckets
set file_size_limit = 10485760,
    allowed_mime_types = array['image/jpeg', 'image/png', 'image/webp']
where id in ('stories', 'avatars', 'post-images');

drop policy if exists "reels_public_read" on storage.objects;
create policy "reels_public_read" on storage.objects
    for select using (bucket_id = 'reels');

drop policy if exists "reels_insert_own_folder" on storage.objects;
create policy "reels_insert_own_folder" on storage.objects
    for insert to authenticated
    with check (bucket_id = 'reels' and (storage.foldername(name))[1] = auth.uid()::text);

drop policy if exists "reels_delete_own_folder" on storage.objects;
create policy "reels_delete_own_folder" on storage.objects
    for delete to authenticated
    using (bucket_id = 'reels' and (storage.foldername(name))[1] = auth.uid()::text);
