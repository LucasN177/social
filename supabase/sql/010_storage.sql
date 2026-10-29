-- 010_storage.sql
-- Storage-Buckets für Profilbilder, Beitragsbilder und Stories.
-- Konvention für Pfade: <user_id>/<dateiname>, damit die Policies den Besitzer erkennen.

insert into storage.buckets (id, name, public)
values
    ('avatars',     'avatars',     true),
    ('post-images', 'post-images', true),
    ('stories',     'stories',     true)
on conflict (id) do nothing;

drop policy if exists "media_public_read" on storage.objects;
create policy "media_public_read" on storage.objects
    for select using (bucket_id in ('avatars', 'post-images', 'stories'));

drop policy if exists "media_insert_own_folder" on storage.objects;
create policy "media_insert_own_folder" on storage.objects
    for insert to authenticated
    with check (
        bucket_id in ('avatars', 'post-images', 'stories')
        and (storage.foldername(name))[1] = auth.uid()::text
    );

drop policy if exists "media_update_own_folder" on storage.objects;
create policy "media_update_own_folder" on storage.objects
    for update to authenticated
    using (
        bucket_id in ('avatars', 'post-images', 'stories')
        and (storage.foldername(name))[1] = auth.uid()::text
    );

drop policy if exists "media_delete_own_folder" on storage.objects;
create policy "media_delete_own_folder" on storage.objects
    for delete to authenticated
    using (
        bucket_id in ('avatars', 'post-images', 'stories')
        and (storage.foldername(name))[1] = auth.uid()::text
    );
