-- 005_follows.sql
-- Social Graph: wer folgt wem

create table if not exists public.follows (
    follower_id  uuid not null references public.users (id) on delete cascade,
    following_id uuid not null references public.users (id) on delete cascade,
    created_at   timestamptz not null default now(),
    primary key (follower_id, following_id),
    check (follower_id <> following_id)
);

create index if not exists follows_following_id_idx on public.follows (following_id);

-- RLS
alter table public.follows enable row level security;

drop policy if exists "follows_select_all" on public.follows;
create policy "follows_select_all" on public.follows
    for select using (true);

drop policy if exists "follows_insert_own" on public.follows;
create policy "follows_insert_own" on public.follows
    for insert with check (auth.uid() = follower_id);

drop policy if exists "follows_delete_own" on public.follows;
create policy "follows_delete_own" on public.follows
    for delete using (auth.uid() = follower_id);
