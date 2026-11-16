-- 021_mentions.sql
-- Erwähnungen (@username) in Posts und Kommentaren erzeugen eine Benachrichtigung
-- vom Typ "mention" (Tabelle und Anzeige gibt es seit 007 / 016). Die App zeigt sie
-- beim nächsten Öffnen bzw. über das Badge-Polling – kein Push, kein Realtime.

-- IDs der erwähnten User (ohne Autor, ohne Duplikate, höchstens 20 Erwähnungen je Text).
-- Ein Punkt am Ende ("@lucas.") gehört meist zum Satz – beide Varianten werden gesucht.
create or replace function public.mentioned_user_ids(p_text text, p_author uuid)
returns setof uuid
language sql
stable
security definer
set search_path = public
as $$
    select distinct u.id
    from (
        select lower(m[1]) as name
        from regexp_matches(coalesce(p_text, ''), '(?:^|[^a-zA-Z0-9._@])@([a-zA-Z0-9._]{3,30})', 'g') as m
        limit 20
    ) x
    join users u on u.username in (x.name, rtrim(x.name, '.'))
    where u.id <> p_author;
$$;

revoke execute on function public.mentioned_user_ids(text, uuid) from public, anon, authenticated;

-- Posts: beim Anlegen alle, beim Bearbeiten nur neu hinzugekommene Erwähnungen
create or replace function public.notify_on_post_mention()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into notifications (user_id, actor_id, type, post_id, message)
    select m.id, new.user_id, 'mention', new.id, 'mentioned you in a post'
    from mentioned_user_ids(new.content, new.user_id) as m(id)
    where tg_op = 'INSERT'
       or m.id not in (select o.id from mentioned_user_ids(old.content, old.user_id) as o(id));
    return null;
end;
$$;

drop trigger if exists posts_mention_notify on public.posts;
create trigger posts_mention_notify
    after insert or update of content on public.posts
    for each row execute function public.notify_on_post_mention();

-- Kommentare: der Post-Autor bekommt schon die Kommentar-Benachrichtigung (007)
create or replace function public.notify_on_comment_mention()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
    v_post_owner uuid;
begin
    select user_id into v_post_owner from posts where id = new.post_id;

    insert into notifications (user_id, actor_id, type, post_id, message)
    select m.id, new.user_id, 'mention', new.post_id, 'mentioned you in a comment'
    from mentioned_user_ids(new.content, new.user_id) as m(id)
    where m.id is distinct from v_post_owner;
    return null;
end;
$$;

drop trigger if exists comments_mention_notify on public.comments;
create trigger comments_mention_notify
    after insert on public.comments
    for each row execute function public.notify_on_comment_mention();
