-- 006_stories.sql
-- Stories (24 Stunden sichtbar) und wer sie gesehen hat

create table if not exists public.stories (
    id         bigint generated always as identity primary key,
    user_id    uuid not null references public.users (id) on delete cascade,
    image_url  text not null,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null default now() + interval '24 hours'
);

create index if not exists stories_user_expires_idx on public.stories (user_id, expires_at desc);

create table if not exists public.story_views (
    story_id  bigint not null references public.stories (id) on delete cascade,
    user_id   uuid not null references public.users (id) on delete cascade,
    viewed_at timestamptz not null default now(),
    primary key (story_id, user_id)
);

-- RLS
alter table public.stories     enable row level security;
alter table public.story_views enable row level security;

drop policy if exists "stories_select_active" on public.stories;
create policy "stories_select_active" on public.stories
    for select using (expires_at > now() or auth.uid() = user_id);

drop policy if exists "stories_insert_own" on public.stories;
create policy "stories_insert_own" on public.stories
    for insert with check (auth.uid() = user_id);

drop policy if exists "stories_delete_own" on public.stories;
create policy "stories_delete_own" on public.stories
    for delete using (auth.uid() = user_id);

-- Viewer sehen ihre eigenen Views, Story-Besitzer sehen alle Views ihrer Story
drop policy if exists "story_views_select" on public.story_views;
create policy "story_views_select" on public.story_views
    for select using (
        auth.uid() = user_id
        or exists (select 1 from public.stories s where s.id = story_id and s.user_id = auth.uid())
    );

drop policy if exists "story_views_insert_own" on public.story_views;
create policy "story_views_insert_own" on public.story_views
    for insert with check (auth.uid() = user_id);
