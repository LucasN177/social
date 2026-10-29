-- 008_counters.sql
-- Hält die denormalisierten Zähler (likes_count, comments_count, posts_count,
-- followers_count, following_count) aktuell. security definer, weil die Zähler
-- in fremden Zeilen geändert werden, für die der User keine UPDATE-Policy hat.

create or replace function public.update_post_likes_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update posts set likes_count = likes_count + 1 where id = new.post_id;
    elsif tg_op = 'DELETE' then
        update posts set likes_count = greatest(likes_count - 1, 0) where id = old.post_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_post_comments_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update posts set comments_count = comments_count + 1 where id = new.post_id;
    elsif tg_op = 'DELETE' then
        update posts set comments_count = greatest(comments_count - 1, 0) where id = old.post_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_comment_likes_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update comments set likes_count = likes_count + 1 where id = new.comment_id;
    elsif tg_op = 'DELETE' then
        update comments set likes_count = greatest(likes_count - 1, 0) where id = old.comment_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_user_posts_count()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update users set posts_count = posts_count + 1 where id = new.user_id;
    elsif tg_op = 'DELETE' then
        update users set posts_count = greatest(posts_count - 1, 0) where id = old.user_id;
    end if;
    return null;
end;
$$;

create or replace function public.update_follow_counts()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if tg_op = 'INSERT' then
        update users set following_count = following_count + 1 where id = new.follower_id;
        update users set followers_count = followers_count + 1 where id = new.following_id;
    elsif tg_op = 'DELETE' then
        update users set following_count = greatest(following_count - 1, 0) where id = old.follower_id;
        update users set followers_count = greatest(followers_count - 1, 0) where id = old.following_id;
    end if;
    return null;
end;
$$;

drop trigger if exists likes_count_trigger on public.likes;
create trigger likes_count_trigger
    after insert or delete on public.likes
    for each row execute function public.update_post_likes_count();

drop trigger if exists comments_count_trigger on public.comments;
create trigger comments_count_trigger
    after insert or delete on public.comments
    for each row execute function public.update_post_comments_count();

drop trigger if exists comment_likes_count_trigger on public.comment_likes;
create trigger comment_likes_count_trigger
    after insert or delete on public.comment_likes
    for each row execute function public.update_comment_likes_count();

drop trigger if exists posts_count_trigger on public.posts;
create trigger posts_count_trigger
    after insert or delete on public.posts
    for each row execute function public.update_user_posts_count();

drop trigger if exists follows_count_trigger on public.follows;
create trigger follows_count_trigger
    after insert or delete on public.follows
    for each row execute function public.update_follow_counts();
