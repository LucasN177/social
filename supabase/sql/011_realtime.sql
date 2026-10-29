-- 011_realtime.sql
-- Realtime-Events für Benachrichtigungen aktivieren (RLS gilt auch für Realtime).

do $$
begin
    if not exists (
        select 1 from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'notifications'
    ) then
        alter publication supabase_realtime add table public.notifications;
    end if;
end;
$$;
