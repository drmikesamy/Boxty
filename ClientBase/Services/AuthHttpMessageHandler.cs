using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Boxty.ClientBase.Services;

public class AuthHttpMessageHandler : DelegatingHandler
{
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<AuthHttpMessageHandler> _logger;
    private int _logoutStarted;

    public AuthHttpMessageHandler(
        NavigationManager navigationManager,
        ILogger<AuthHttpMessageHandler> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Handle authentication failures
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized response, redirecting to logout");
                Logout();
            }

            return response;
        }
        catch (AccessTokenNotAvailableException ex)
        {
            _logger.LogWarning("Access token not available, redirecting to logout: {Message}", ex.Message);
            Logout();
            throw;
        }
    }

    private void Logout()
    {
        if (Interlocked.Exchange(ref _logoutStarted, 1) == 0)
        {
            _navigationManager.NavigateToLogout("authentication/logout");
        }
    }
}