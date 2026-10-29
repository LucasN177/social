-- 004_interactions.sql
-- Likes und Lesezeichen auf Beiträge

create table if not exists public.likes (
    post_id    bigint not null references public.posts (id) on delete cascade,
    user_id    uuid not null references public.users (id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (post_id, user_id)
);

create index if not exists likes_user_id_idx on public.likes (user_id);

create table if not exists public.bookmarks (
    post_id    bigint not null references public.posts (id) on delete cascade,
    user_id    uuid not null references public.users (id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (post_id, user_id)
);

create index if not exists bookmarks_user_id_idx on public.bookmarks (user_id, created_at desc);

-- RLS
alter table public.likes     enable row level security;
alter table public.bookmarks enable row level security;

drop policy if exists "likes_select_all" on public.likes;
create policy "likes_select_all" on public.likes
    for select using (true);

drop policy if exists "likes_insert_own" on public.likes;
create policy "likes_insert_own" on public.likes
    for insert with check (auth.uid() = user_id);

drop policy if exists "likes_delete_own" on public.likes;
create policy "likes_delete_own" on public.likes
    for delete using (auth.uid() = user_id);

-- Lesezeichen sind privat
drop policy if exists "bookmarks_own" on public.bookmarks;
create policy "bookmarks_own" on public.bookmarks
    for all using (auth.uid() = user_id) with check (auth.uid() = user_id);
