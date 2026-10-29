using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Client = Supabase.Client;

namespace Social.Services;

/// <summary>
/// Stellt den Supabase-Login als Blazor-AuthenticationState bereit,
/// damit [Authorize] und AuthorizeRouteView funktionieren.
/// </summary>
public class SupabaseAuthStateProvider : AuthenticationStateProvider
{
    private readonly Client _client;

    public SupabaseAuthStateProvider(Client client)
    {
        _client = client;
        _client.Auth.AddStateChangedListener(OnAuthStateChanged);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _client.Auth.CurrentUser;
        if (user is null || _client.Auth.CurrentSession is null)
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };
        var identity = new ClaimsIdentity(claims, "supabase");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void OnAuthStateChanged(IGotrueClient<User, Session> sender, Constants.AuthState state)
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
