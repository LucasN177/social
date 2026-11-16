using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor;
using Social.Core.Interfaces.Services;
using Social.Core.Models.Response;
using Social.Services;

namespace Social.Tests.Services;

public class FeedbackServiceTests
{
    private readonly Mock<ISnackbar> _snackbar = new();

    private FeedbackService CreateService() => new(_snackbar.Object, NullLogger<FeedbackService>.Instance);

    private void VerifyShown(string message, Severity severity) =>
        _snackbar.Verify(s => s.Add(message, severity, It.IsAny<Action<SnackbarOptions>?>(), It.IsAny<string?>()), Times.Once);

    [Fact]
    public void Error_ShowsServiceMessage()
    {
        CreateService().Error(Response.Failure("Your comment could not be posted.", null), "fallback");

        VerifyShown("Your comment could not be posted.", Severity.Error);
    }

    [Fact]
    public void Error_HidesTechnicalExceptionMessages()
    {
        var exception = new Exception("{\"code\":\"42501\",\"message\":\"permission denied for table posts\"}");

        CreateService().Error(Response.Failure(exception.Message, exception), "The post could not be deleted.");

        VerifyShown("The post could not be deleted.", Severity.Error);
    }

    [Fact]
    public void Error_UsesFallback_WhenMessageMissing()
    {
        CreateService().Error(Response<int>.Failure(new InvalidOperationException("boom")));

        VerifyShown(IFeedbackService.DefaultError, Severity.Error);
    }

    [Fact]
    public void Error_ShowsOfflineHint_ForNetworkErrors()
    {
        var exception = new HttpRequestException("No such host is known.");

        CreateService().Error(Response.Failure(exception.Message, exception));

        VerifyShown(IFeedbackService.OfflineError, Severity.Error);
    }

    [Fact]
    public void Handle_ShowsSuccessMessage_AndReturnsTrue()
    {
        var handled = CreateService().Handle(Response.Success(), "Saved");

        Assert.True(handled);
        VerifyShown("Saved", Severity.Success);
    }

    [Fact]
    public void Handle_IsSilentOnSuccess_WithoutMessage()
    {
        Assert.True(CreateService().Handle(Response.Success()));
        _snackbar.VerifyNoOtherCalls();
    }

    [Fact]
    public void UserMessage_IsNull_ForSuccess() =>
        Assert.Null(CreateService().UserMessage(Response.Success()));
}
