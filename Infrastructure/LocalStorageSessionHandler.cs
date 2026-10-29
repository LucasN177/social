using Microsoft.JSInterop;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Social.Infrastructure;

/// <summary>
/// Speichert die Supabase-Session im localStorage, damit der Login einen Reload übersteht.
/// Gotrue ruft die Methoden synchron auf, daher wird IJSInProcessRuntime genutzt (nur in WASM verfügbar).
/// </summary>
public class LocalStorageSessionHandler(IJSRuntime jsRuntime) : IGotrueSessionPersistence<Session>
{
    private const string StorageKey = "social.supabase.session";

    private IJSInProcessRuntime Js => (IJSInProcessRuntime)jsRuntime;

    public void SaveSession(Session session)
    {
        Js.InvokeVoid("localStorage.setItem", StorageKey, JsonConvert.SerializeObject(session));
    }

    public void DestroySession()
    {
        Js.InvokeVoid("localStorage.removeItem", StorageKey);
    }

    public Session? LoadSession()
    {
        var json = Js.Invoke<string?>("localStorage.getItem", StorageKey);
        return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Session>(json);
    }
}
