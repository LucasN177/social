-- 003_comments.sql
-- Kommentare zu Beiträgen und Likes auf Kommentare

create table if not exists public.comments (
    id          bigint generated always as identity primary key,
    post_id     bigint not null references public.posts (id) on delete cascade,
    user_id     uuid not null references public.users (id) on delete cascade,
    content     text not null check (char_length(content) between 1 and 2000),
    likes_count integer not null default 0,
    created_at  timestamptz not null default now()
);

create index if not exists comments_post_id_idx on public.comments (post_id, created_at);

create table if not exists public.comment_likes (
    comment_id bigint not null references public.comments (id) on delete cascade,
    user_id    uuid not null references public.users (id) on delete cascade,
    created_at timestamptz not null default now(),
    primary key (comment_id, user_id)
);

-- RLS
alter table public.comments      enable row level security;
alter table public.comment_likes enable row level security;

drop policy if exists "comments_select_all" on public.comments;
create policy "comments_select_all" on public.comments
    for select using (true);

drop policy if exists "comments_insert_own" on public.comments;
create policy "comments_insert_own" on public.comments
    for insert with check (auth.uid() = user_id);

drop policy if exists "comments_delete_own" on public.comments;
create policy "comments_delete_own" on public.comments
    for delete using (
        auth.uid() = user_id
        or exists (select 1 from public.posts p where p.id = post_id and p.user_id = auth.uid())
    );

drop policy if exists "comment_likes_select_all" on public.comment_likes;
create policy "comment_likes_select_all" on public.comment_likes
    for select using (true);

drop policy if exists "comment_likes_insert_own" on public.comment_likes;
create policy "comment_likes_insert_own" on public.comment_likes
    for insert with check (auth.uid() = user_id);

drop policy if exists "comment_likes_delete_own" on public.comment_likes;
create policy "comment_likes_delete_own" on public.comment_likes
    for delete using (auth.uid() = user_id);
