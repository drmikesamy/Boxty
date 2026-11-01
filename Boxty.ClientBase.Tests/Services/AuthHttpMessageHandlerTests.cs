using System.Net;
using Boxty.ClientBase.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Boxty.ClientBase.Tests.Services;

public class AuthHttpMessageHandlerTests
{
    [Fact]
    public async Task TokenAcquisitionFailureLogsOutBeforeReachingApi()
    {
        var navigation = new TestNavigationManager();
        var tokenProvider = new Mock<IAccessTokenProvider>();
        var result = new AccessTokenResult(
            AccessTokenResultStatus.RequiresRedirect,
            new AccessToken(),
            "authentication/login",
            new InteractiveRequestOptions { Interaction = InteractionType.GetToken, ReturnUrl = navigation.Uri });
        tokenProvider.Setup(provider => provider.RequestAccessToken()).ReturnsAsync(result);
        tokenProvider.Setup(provider => provider.RequestAccessToken(It.IsAny<AccessTokenRequestOptions>())).ReturnsAsync(result);
        var transport = new StubHandler();
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(navigation);
        services.AddSingleton(tokenProvider.Object);
        services.AddTransient<AuthHttpMessageHandler>();
        services.AddTransient<AuthorizationMessageHandler>();
        services.AddHttpClient("api", client => client.BaseAddress = new Uri("https://api.test/"))
            .AddHttpMessageHandler<AuthHttpMessageHandler>()
            .AddHttpMessageHandler(provider => provider.GetRequiredService<AuthorizationMessageHandler>()
                .ConfigureHandler(authorizedUrls: ["https://api.test/"]))
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("api");

        await Assert.ThrowsAsync<AccessTokenNotAvailableException>(() => client.GetAsync("api/subject/getall"));

        Assert.Equal("https://app.test/authentication/logout", Assert.Single(navigation.Navigations));
        Assert.Equal(0, transport.RequestCount);
    }

    [Fact]
    public async Task UnauthorizedResponsesInitiateOnlyOneLogout()
    {
        var navigation = new TestNavigationManager();
        using var client = CreateClient(navigation, new StubHandler { StatusCode = HttpStatusCode.Unauthorized });

        using var first = await client.GetAsync("https://api.test/api/subject/getall");
        using var second = await client.GetAsync("https://api.test/api/role/getall");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal("https://app.test/authentication/logout", Assert.Single(navigation.Navigations));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task OtherResponsesDoNotLogOut(HttpStatusCode statusCode)
    {
        var navigation = new TestNavigationManager();
        using var client = CreateClient(navigation, new StubHandler { StatusCode = statusCode });

        using var response = await client.GetAsync("https://api.test/api/subject/getall");

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Empty(navigation.Navigations);
    }

    [Theory]
    [InlineData("Request failed with 400")]
    [InlineData("Network unavailable")]
    [InlineData("invalid_token in validation response")]
    public async Task HttpErrorsArePropagatedWithoutGuessingAuthenticationFromMessage(string message)
    {
        var navigation = new TestNavigationManager();
        var error = new HttpRequestException(message);
        using var client = CreateClient(navigation, new StubHandler { Error = error });

        var actual = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://api.test/api/subject/getall"));

        Assert.Same(error, actual);
        Assert.Empty(navigation.Navigations);
    }

    private static HttpClient CreateClient(TestNavigationManager navigation, HttpMessageHandler inner) =>
        new(new AuthHttpMessageHandler(navigation, NullLogger<AuthHttpMessageHandler>.Instance) { InnerHandler = inner });

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public Exception? Error { get; init; }
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Error == null
                ? Task.FromResult(new HttpResponseMessage(StatusCode))
                : Task.FromException<HttpResponseMessage>(Error);
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public List<string> Navigations { get; } = [];

        public TestNavigationManager() => Initialize("https://app.test/", "https://app.test/subjects");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            Assert.NotNull(options.HistoryEntryState);
            Navigations.Add(ToAbsoluteUri(uri).AbsoluteUri);
        }
    }
}
