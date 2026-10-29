-- 002_posts.sql
-- Beiträge und zugehörige Bilder / Videos

create table if not exists public.posts (
    id             bigint generated always as identity primary key,
    user_id        uuid not null references public.users (id) on delete cascade,
    content        text not null default '',
    location       text not null default '',
    tags           text[] not null default '{}',
    media_type     text not null default 'image' check (media_type in ('image', 'video', 'text')),
    likes_count    integer not null default 0,
    comments_count integer not null default 0,
    shares_count   integer not null default 0,
    created_at     timestamptz not null default now(),
    updated_at     timestamptz not null default now()
);

create index if not exists posts_user_id_idx    on public.posts (user_id);
create index if not exists posts_created_at_idx on public.posts (created_at desc);
create index if not exists posts_tags_idx       on public.posts using gin (tags);

drop trigger if exists posts_set_updated_at on public.posts;
create trigger posts_set_updated_at
    before update on public.posts
    for each row execute function public.set_updated_at();

create table if not exists public.post_images (
    id         bigint generated always as identity primary key,
    post_id    bigint not null references public.posts (id) on delete cascade,
    url        text not null,
    position   smallint not null default 0,
    created_at timestamptz not null default now()
);

create index if not exists post_images_post_id_idx on public.post_images (post_id, position);

-- RLS
alter table public.posts       enable row level security;
alter table public.post_images enable row level security;

drop policy if exists "posts_select_all" on public.posts;
create policy "posts_select_all" on public.posts
    for select using (true);

drop policy if exists "posts_insert_own" on public.posts;
create policy "posts_insert_own" on public.posts
    for insert with check (auth.uid() = user_id);

drop policy if exists "posts_update_own" on public.posts;
create policy "posts_update_own" on public.posts
    for update using (auth.uid() = user_id) with check (auth.uid() = user_id);

drop policy if exists "posts_delete_own" on public.posts;
create policy "posts_delete_own" on public.posts
    for delete using (auth.uid() = user_id);

drop policy if exists "post_images_select_all" on public.post_images;
create policy "post_images_select_all" on public.post_images
    for select using (true);

drop policy if exists "post_images_write_own" on public.post_images;
create policy "post_images_write_own" on public.post_images
    for all
    using (exists (select 1 from public.posts p where p.id = post_id and p.user_id = auth.uid()))
    with check (exists (select 1 from public.posts p where p.id = post_id and p.user_id = auth.uid()));
