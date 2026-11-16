-- 022_chat_fixes.sql
-- Korrekturen, gefunden durch die SQL-Tests (supabase/tests).

-- 1) Erste Story-Antwort zählte beim Empfänger nicht als ungelesen: reply_to_story legt
--    Unterhaltung (last_read_at = now()) und Nachricht (created_at = now()) in derselben
--    Transaktion an, "created_at > last_read_at" war nie wahr. Neue Mitglieder haben noch
--    nichts gelesen – deshalb last_read_at = '-infinity'.
create or replace function public.get_or_create_direct_conversation(p_other uuid)
returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
    me      uuid := auth.uid();
    conv_id bigint;
begin
    if me is null then
        raise exception 'not authenticated';
    end if;
    if p_other is null or p_other = me then
        raise exception 'invalid conversation partner';
    end if;
    if not exists (select 1 from users where id = p_other) then
        raise exception 'user not found';
    end if;

    select a.conversation_id into conv_id
    from conversation_members a
    join conversation_members b on b.conversation_id = a.conversation_id and b.user_id = p_other
    join conversations c on c.id = a.conversation_id and not c.is_group
    where a.user_id = me
      and (select count(*) from conversation_members m where m.conversation_id = a.conversation_id) = 2
    limit 1;

    if conv_id is null then
        insert into conversations default values returning id into conv_id;
        insert into conversation_members (conversation_id, user_id, last_read_at)
        values (conv_id, me, '-infinity'), (conv_id, p_other, '-infinity');
    end if;

    return conv_id;
end;
$$;

-- 2) Beim UPDATE durfte image_path auf das Bild eines anderen Mitglieds derselben
--    Unterhaltung zeigen. Jetzt gelten dieselben Regeln wie beim INSERT (eigener Ordner).
drop policy if exists "messages_update_own" on public.messages;
create policy "messages_update_own" on public.messages
    for update
    using (sender_id = auth.uid())
    with check (
        sender_id = auth.uid()
        and (
            image_path is null
            or (split_part(image_path, '/', 1) = conversation_id::text
                and split_part(image_path, '/', 2) = auth.uid()::text)
        )
    );
