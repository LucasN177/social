using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using User = Supabase.Gotrue.User;
using Task = System.Threading.Tasks.Task;

namespace Social.Infrastructure;

public class AuthRepository(Supabase.Client client, IGotrueSessionPersistence<Session> sessionPersistence): IAuthRepository
{
    public async Task<IResponse<Session>> Login(string username, string password)
    {
        try
        {
            var result = await client.Auth.SignInWithPassword(username, password);
            return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Login failed");
        }
        catch (GotrueException ex)
        {
            return Response<Session>.Failure(GetErrorMessage(ex));
        }
        catch (HttpRequestException)
        {
            return Response<Session>.Failure(ServerUnreachable);
        }
    }

    public async Task<IResponse<Session>> Register(string username, string password, UserDto profile)
    {
        try
        {
            // Die Metadaten liest der Trigger handle_new_user (supabase/sql/001_users.sql) und legt damit die users-Zeile an.
            // So klappt die Registrierung auch, wenn Supabase erst eine E-Mail-Bestätigung verlangt und noch keine Session existiert.
            var options = new SignUpOptions
            {
                Data = new Dictionary<string, object>
                {
                    ["username"] = profile.Username,
                    ["vorname"] = profile.Vorname,
                    ["nachname"] = profile.Nachname,
                    ["display_name"] = $"{profile.Vorname} {profile.Nachname}".Trim()
                }
            };
            var result = await client.Auth.SignUp(username, password, options);
            return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Registration failed");
        }
        catch (GotrueException ex)
        {
            return Response<Session>.Failure(GetErrorMessage(ex));
        }
        catch (HttpRequestException)
        {
            return Response<Session>.Failure(ServerUnreachable);
        }
    }

    public async Task<IResponse> Logout()
    {
        try
        {
            await client.Auth.SignOut();
            return Response.Success();
        }
        catch (Exception ex)
        {
            // Server nicht erreichbar: Gotrue räumt dann nicht auf, also die gespeicherte Session selbst löschen
            sessionPersistence.DestroySession();
            return Response.Failure("Signed out locally only", ex);
        }
    }

    public Task<IResponse<Session>> GetCurrentSession()
    {
        try
        {
            return Task.FromResult(client.Auth.CurrentSession is not null ? Response<Session>.Success(client.Auth.CurrentSession) : Response<Session>.Failure("No login available"));
        }
        catch (Exception exception)
        {
            return Task.FromException<IResponse<Session>>(exception);
        }
    }

    public Task<IResponse<User>> GetCurrentUser()
    {
        try
        {
            return Task.FromResult(client.Auth.CurrentUser is not null ? Response<User>.Success(client.Auth.CurrentUser) : Response<User>.Failure("No login available"));
        }
        catch (Exception exception)
        {
            return Task.FromException<IResponse<User>>(exception);
        }
    }

    public async Task<IResponse> InsertUserMetadata(UserDto user)
    {
        try
        {
            var currentUserId = client.Auth.CurrentUser?.Id;
            if (currentUserId is null)
                return Response.Failure("No login available", null);

            user.UserId = currentUserId;
            await client.From<UserDto>()
                .Where(x => x.UserId == currentUserId)
                .Set(x => x.Username, user.Username)
                .Set(x => x.Vorname, user.Vorname)
                .Set(x =>x.Nachname, user.Nachname)
                .Update();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public Task<IResponse> UpdateUserMetadata(UserDto user)
    {
        throw new NotImplementedException();
    }

    public async Task<IResponse<UserDto>> GetUserMetadata(string id)
    {
        var result = await client.From<UserDto>().Where(x => x.UserId == id).Get();
        return result.Model is not null ? Response<UserDto>.Success(result.Model) : Response<UserDto>.Failure("No user found");
    }

    private const string ServerUnreachable = "Server not reachable. Please check your connection.";

    private static string GetErrorMessage(GotrueException ex) => ex.Reason switch
    {
        FailureHint.Reason.UserBadLogin or FailureHint.Reason.UserBadPassword or FailureHint.Reason.UserBadMultiple
            => "Invalid email or password.",
        FailureHint.Reason.UserEmailNotConfirmed => "Please confirm your email address first.",
        FailureHint.Reason.UserAlreadyRegistered => "An account with this email already exists.",
        FailureHint.Reason.UserBadEmailAddress => "Please enter a valid email address.",
        FailureHint.Reason.UserTooManyRequests => "Too many attempts. Please try again later.",
        FailureHint.Reason.Offline => "You are offline. Please check your connection.",
        _ => "Something went wrong. Please try again."
    };
}
