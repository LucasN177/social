using Social.Core.Interfaces.Response;

namespace Social.Core.Models.Response;

public class Response<T> : Response, IResponse<T>
{
    public T? Data { get; init; }

    private Response()
    {
        IsSuccess = false;
        Date = DateTime.Now;
    }

    public static IResponse<T> Success(T data)
        => new Response<T>()
        {
            IsSuccess = true,
            Data = data,
            Date = DateTime.Now,
        };

    public static IResponse<T> Failure(Exception? exception)
        => new Response<T>()
        {
            IsSuccess = false,
            Date = DateTime.Now,
            Exception = exception
        };
    
    public static IResponse<T> Failure(string errorMessage)
        => new Response<T>()
        {
            IsSuccess = false,
            Date = DateTime.Now,
            ErrorMessage =  errorMessage
        };
}