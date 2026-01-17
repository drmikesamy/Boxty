using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Boxty.ClientBase.Services;

public class AuthHttpMessageHandler : DelegatingHandler
{
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<AuthHttpMessageHandler> _logger;

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
                _navigationManager.NavigateToLogout("authentication/logout");
            }

            return response;
        }
        catch (AccessTokenNotAvailableException ex)
        {
            // This is thrown when token refresh fails
            _logger.LogWarning("Access token not available, redirecting to authentication: {Message}", ex.Message);
            ex.Redirect();
            throw;
        }
        catch (HttpRequestException ex) when (IsTokenEndpointError(ex))
        {
            // Handle token endpoint errors (400, 401, etc.)
            _logger.LogWarning("Token endpoint error detected, redirecting to logout: {Message}", ex.Message);
            _navigationManager.NavigateToLogout("authentication/logout");
            throw;
        }
    }

    private static bool IsTokenEndpointError(HttpRequestException ex)
    {
        // Check if the error is related to token endpoint
        return ex.Message.Contains("/protocol/openid-connect/token") ||
               ex.Message.Contains("400") ||
               ex.Message.Contains("invalid_grant") ||
               ex.Message.Contains("invalid_token");
    }
}