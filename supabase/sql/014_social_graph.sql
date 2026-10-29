-- 014_social_graph.sql
-- Phase 3: Home-Feed aus gefolgten Accounts, Hashtag-Suche, Follower-Listen.

-- Home-Feed: eigene Posts + Posts von Accounts, denen man folgt.
-- feed_posts (013) ist selbst security_invoker, RLS greift also weiterhin.
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

-- Hashtags nach Präfix suchen, sortiert nach Häufigkeit
create or replace function public.search_hashtags(term text, max_results integer default 20)
returns table (tag text, post_count bigint)
language sql
stable
security invoker
set search_path = public
as $$
    select t.tag, count(*) as post_count
    from posts p
    cross join lateral unnest(p.tags) as t(tag)
    where t.tag like replace(lower(trim(term)), '#', '') || '%'
    group by t.tag
    order by count(*) desc, t.tag
    limit max_results;
$$;

-- Follower / Gefolgte eines Users (für die Listen im Profil)
create or replace function public.list_followers(target uuid, max_results integer default 50)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from follows f
    join users u on u.id = f.follower_id
    where f.following_id = target
    order by f.created_at desc
    limit max_results;
$$;

create or replace function public.list_following(target uuid, max_results integer default 50)
returns setof public.users
language sql
stable
security invoker
set search_path = public
as $$
    select u.*
    from follows f
    join users u on u.id = f.following_id
    where f.follower_id = target
    order by f.created_at desc
    limit max_results;
$$;

-- Hashtag-Filter im Feed beschleunigen (Index existiert seit 002, hier nur zur Sicherheit)
create index if not exists posts_tags_idx on public.posts using gin (tags);
