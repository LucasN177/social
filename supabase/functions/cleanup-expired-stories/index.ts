// Edge Function: löscht abgelaufene Stories samt Bild-/Videodatei aus dem Bucket "stories".
// Aufruf stündlich per pg_cron (siehe supabase/sql/019_stories_media_replies_cleanup.sql).
// Deploy: supabase functions deploy cleanup-expired-stories --no-verify-jwt
//
// Ohne JWT-Prüfung aufrufbar, weil pg_cron keinen User-Token hat. Die Funktion löscht
// ausschließlich Stories, deren expires_at vorbei ist – ein zusätzlicher Aufruf schadet nicht.
// SUPABASE_URL und SUPABASE_SERVICE_ROLE_KEY stellt Supabase automatisch bereit.

import { createClient } from "jsr:@supabase/supabase-js@2";

const BUCKET = "stories";
const BATCH_SIZE = 500;
const MAX_BATCHES = 20;

/** Pfad im Bucket aus der öffentlichen URL, null für fremde URLs. */
function storagePath(publicUrl: string): string | null {
  const marker = `/storage/v1/object/public/${BUCKET}/`;
  const index = publicUrl.indexOf(marker);
  if (index < 0) return null;
  const path = decodeURIComponent(publicUrl.slice(index + marker.length).split("?")[0]);
  return path.length > 0 ? path : null;
}

Deno.serve(async () => {
  const supabase = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!,
    { auth: { persistSession: false } },
  );

  let deletedStories = 0;
  let deletedFiles = 0;

  for (let batch = 0; batch < MAX_BATCHES; batch++) {
    const { data: stories, error } = await supabase
      .from("stories")
      .select("id, image_url")
      .lt("expires_at", new Date().toISOString())
      .order("expires_at", { ascending: true })
      .limit(BATCH_SIZE);

    if (error) return json({ error: error.message }, 500);
    if (!stories || stories.length === 0) break;

    // Erst die Dateien, dann die Zeilen – schlägt das Löschen der Dateien fehl,
    // bleiben die Zeilen erhalten und der nächste Lauf versucht es erneut.
    const paths = stories
      .map((s) => storagePath(s.image_url ?? ""))
      .filter((p): p is string => p !== null);

    if (paths.length > 0) {
      const { error: removeError } = await supabase.storage.from(BUCKET).remove(paths);
      if (removeError) return json({ error: removeError.message, deletedStories, deletedFiles }, 500);
      deletedFiles += paths.length;
    }

    const ids = stories.map((s) => s.id);
    const { error: deleteError } = await supabase.from("stories").delete().in("id", ids);
    if (deleteError) return json({ error: deleteError.message, deletedStories, deletedFiles }, 500);
    deletedStories += ids.length;

    if (stories.length < BATCH_SIZE) break;
  }

  return json({ deletedStories, deletedFiles });
});

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
