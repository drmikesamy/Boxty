using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using MudBlazor;
using Microsoft.AspNetCore.Components.Authorization;
using Boxty.SharedBase.DTOs.Auth;
using Boxty.SharedBase.Interfaces;

namespace Boxty.ClientBase.Services
{
    public interface IAuthHelperService
    {
        Task<string?> GetFullNameAsync();
        Task<string?> GetEmailAsync();
        Task<string?> GetUserNameAsync();
        Task<Guid?> GetSubjectIdAsync();
        Task<Guid?> GetTenantIdAsync();
        Task<ClaimsPrincipal?> GetUserAsync();
        Task<TSubjectDto?> ResetSubjectPassword<TSubjectDto>(Guid id, CancellationToken token) where TSubjectDto : class, ISubject;
        Task<bool> IsAuthorizedForRoleManagement();
        Task<bool> IsUserInRole(string role);
        Task<IEnumerable<RoleDto>> GetAllRolesAsync();
    }

    public class AuthHelperService : IAuthHelperService
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly HttpClient _httpClient;
        private readonly ISnackbar _snackbar;
        private readonly GlobalStateService _globalStateService;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public AuthHelperService(AuthenticationStateProvider authenticationStateProvider, HttpClient httpClient, ISnackbar snackbar, GlobalStateService globalStateService)
        {
            _authenticationStateProvider = authenticationStateProvider;
            _httpClient = httpClient;
            _snackbar = snackbar;
            _globalStateService = globalStateService;
        }
        public async Task<ClaimsPrincipal?> GetUserAsync()
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            return authState.User.Identity?.IsAuthenticated == true ? authState.User : null;
        }

        public async Task<string?> GetFullNameAsync()
        {
            var user = await GetUserAsync();
            return user?.FindFirst("name")?.Value;
        }

        public async Task<string?> GetEmailAsync()
        {
            var user = await GetUserAsync();
            return user?.FindFirst("email")?.Value;
        }

        public async Task<string?> GetUserNameAsync()
        {
            var user = await GetUserAsync();
            return user?.FindFirst("preferred_username")?.Value;
        }

        private void HandleErrorResponse(string errorContent)
        {
            try
            {
                // Try to parse the error response as JSON
                var errorResponse = JsonSerializer.Deserialize<JsonElement>(errorContent);

                string errorMessage = "An error occurred";

                // Check if it's a validation error with multiple errors
                if (errorResponse.TryGetProperty("errors", out var errorsProperty) && errorsProperty.ValueKind == JsonValueKind.Array)
                {
                    var errors = new List<string>();
                    foreach (var error in errorsProperty.EnumerateArray())
                    {
                        if (error.TryGetProperty("message", out var messageProperty))
                        {
                            errors.Add(messageProperty.GetString() ?? "Unknown error");
                        }
                    }
                    errorMessage = string.Join(", ", errors);
                }
                // Check if it's a simple message property
                else if (errorResponse.TryGetProperty("message", out var messageProperty))
                {
                    errorMessage = messageProperty.GetString() ?? "Unknown error";
                }
                // If no structured error, use the raw content
                else
                {
                    errorMessage = errorContent;
                }

                _snackbar.Add(errorMessage, Severity.Error);
            }
            catch
            {
                // If JSON parsing fails, show the raw error content
                _snackbar.Add(!string.IsNullOrEmpty(errorContent) ? errorContent : "An unknown error occurred", Severity.Error);
            }
        }

        public async Task<TSubjectDto?> ResetSubjectPassword<TSubjectDto>(Guid id, CancellationToken token)
            where TSubjectDto : class, ISubject
        {
            try
            {
                _globalStateService.StartLoading();
                var response = await _httpClient.PutAsync($"api/{typeof(TSubjectDto).Name.ToLowerInvariant().Replace("dto", "")}/resetpassword/{id}", null, token);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(token);
                    var subject = JsonSerializer.Deserialize<TSubjectDto>(json, JsonOptions);
                    _snackbar.Add($"Successfully reset password for subject", Severity.Success);
                    return subject;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(token);
                    HandleErrorResponse(errorContent);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _snackbar.Add($"Network error: {ex.Message}", Severity.Error);
                throw;
            }
            catch (TaskCanceledException)
            {
                _snackbar.Add("Request timed out", Severity.Error);
                throw;
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Unexpected error: {ex.Message}", Severity.Error);
                throw;
            }
            finally
            {
                _globalStateService.StopLoading();
            }
        }

        public async Task<bool> IsAuthorizedForRoleManagement()
        {
            var user = await GetUserAsync();
            if (user == null) return false;

            return user.IsInRole("administrator") || user.IsInRole("tenantadministrator");
        }

        public async Task<bool> IsUserInRole(string role)
        {
            var user = await GetUserAsync();
            if (user == null) return false;

            return user.IsInRole(role);
        }

        public async Task<Guid?> GetSubjectIdAsync()
        {
            var user = await GetUserAsync();
            if (user == null) return null;

            var subjectIdClaimParsed = Guid.TryParse(user.FindFirst("sub")?.Value, out var subjectId);
            return subjectIdClaimParsed ? subjectId : null;
        }

        public async Task<Guid?> GetTenantIdAsync()
        {
            var user = await GetUserAsync();
            if (user == null) return null;

            var orgClaim = user.FindFirst("organization")?.Value;
            if (string.IsNullOrEmpty(orgClaim))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(orgClaim);
                var root = doc.RootElement;
                // Handle array format (existing logic)
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstObj = root[0];
                    if (firstObj.TryGetProperty("id", out var directIdProp))
                    {
                        var tenantIdClaimParsed = Guid.TryParse(directIdProp.GetString(), out var tenantId);
                        return tenantIdClaimParsed ? tenantId : null;
                    }
                }
                // Handle object format (new logic for nested structure)
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Object &&
                            property.Value.TryGetProperty("id", out var nestedIdProp))
                        {
                            var tenantIdClaimParsed = Guid.TryParse(nestedIdProp.GetString(), out var tenantId);
                            return tenantIdClaimParsed ? tenantId : null;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<RoleDto>>("api/auth/roles/getall") ?? new();
        }
    }
}
