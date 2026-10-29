-- 012_users_profile_rules.sql
-- Die Policy users_update_own (001) erlaubt das Ändern der eigenen Zeile – ohne diese Rechte
-- könnte ein Client auch followers_count, posts_count oder is_verified setzen.
-- Nur die Profilfelder bleiben für eingeloggte User änderbar; die Zähler-Trigger (008)
-- laufen als security definer und sind davon nicht betroffen.

revoke update on public.users from anon, authenticated;
grant update (username, display_name, vorname, nachname, bio, profile_image_url)
    on public.users to authenticated;

-- Format-Regeln wie in der App (Pages/Login.razor, Pages/Dialogs/EditProfileDialog.razor).
-- "not valid": bestehende Zeilen werden nicht geprüft, nur neue bzw. geänderte.
do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'users_username_format') then
        alter table public.users
            add constraint users_username_format check (username ~ '^[a-z0-9._]{3,30}$') not valid;
    end if;

    if not exists (select 1 from pg_constraint where conname = 'users_bio_length') then
        alter table public.users
            add constraint users_bio_length check (char_length(bio) <= 160) not valid;
    end if;
end;
$$;
