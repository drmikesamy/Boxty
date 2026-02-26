using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using MudBlazor;
using Microsoft.AspNetCore.Components.Authorization;
using Boxty.SharedBase.DTOs.Auth;
using Boxty.SharedBase.Interfaces;

namespace Boxty.ClientBase.Services
{
    public class TenantSelectionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public interface IAuthHelperService
    {
        Task<string?> GetFullNameAsync();
        Task<string?> GetEmailAsync();
        Task<string?> GetUserNameAsync();
        Task<Guid?> GetSubjectIdAsync();
        Task<Guid?> GetTenantIdAsync();
        Task<Guid?> GetActiveTenantIdAsync();
        Task SetActiveTenantIdAsync(Guid? tenantId);
        Task<IEnumerable<TenantSelectionDto>> GetAvailableTenantsAsync();
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
        private readonly ILocalStorageService? _localStorageService;
        private const string ActiveTenantStorageKey = "boxty.active.tenant.id";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public AuthHelperService(AuthenticationStateProvider authenticationStateProvider, HttpClient httpClient, ISnackbar snackbar, GlobalStateService globalStateService)
            : this(authenticationStateProvider, httpClient, snackbar, globalStateService, null)
        {
        }

        public AuthHelperService(AuthenticationStateProvider authenticationStateProvider, HttpClient httpClient, ISnackbar snackbar, GlobalStateService globalStateService, ILocalStorageService? localStorageService)
        {
            _authenticationStateProvider = authenticationStateProvider;
            _httpClient = httpClient;
            _snackbar = snackbar;
            _globalStateService = globalStateService;
            _localStorageService = localStorageService;
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
            var activeTenantId = await GetActiveTenantIdAsync();
            if (activeTenantId.HasValue)
            {
                return activeTenantId;
            }

            var tenants = (await GetAvailableTenantsAsync()).ToList();
            if (tenants.Count == 0)
            {
                return null;
            }

            var fallbackTenantId = tenants[0].Id;
            await SetActiveTenantIdAsync(fallbackTenantId);
            return fallbackTenantId;
        }

        public async Task<Guid?> GetActiveTenantIdAsync()
        {
            if (_localStorageService == null)
            {
                return await GetClaimTenantIdAsync();
            }

            try
            {
                var storedTenant = await _localStorageService.GetItemAsync<string>(ActiveTenantStorageKey);
                if (Guid.TryParse(storedTenant, out var activeTenantId))
                {
                    var tenants = await GetAvailableTenantsAsync();
                    if (tenants.Any(t => t.Id == activeTenantId))
                    {
                        return activeTenantId;
                    }
                }
            }
            catch
            {
            }

            return await GetClaimTenantIdAsync();
        }

        public async Task SetActiveTenantIdAsync(Guid? tenantId)
        {
            if (_localStorageService == null)
            {
                return;
            }

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                await _localStorageService.RemoveItemAsync(ActiveTenantStorageKey);
                return;
            }

            await _localStorageService.SetItemAsync(ActiveTenantStorageKey, tenantId.Value.ToString());
        }

        public async Task<IEnumerable<TenantSelectionDto>> GetAvailableTenantsAsync()
        {
            var user = await GetUserAsync();
            if (user == null) return [];

            var orgClaim = user.FindFirst("organization")?.Value;
            if (string.IsNullOrEmpty(orgClaim))
                return [];

            var tenants = new List<TenantSelectionDto>();

            try
            {
                using var doc = JsonDocument.Parse(orgClaim);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        AddTenantFromJsonElement(tenants, item);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("id", out _))
                    {
                        AddTenantFromJsonElement(tenants, root);
                    }
                    else
                    {
                        foreach (var property in root.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.Object)
                            {
                                AddTenantFromJsonElement(tenants, property.Value, property.Name);
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            if (tenants.Count == 0 && (user.IsInRole("administrator") || user.IsInRole("tenantadministrator") || user.IsInRole("tenantlimitedadministrator")))
            {
                var tenantsFromApi = await GetTenantsFromApiAsync();
                tenants.AddRange(tenantsFromApi);
            }

            return tenants
                .Where(t => t.Id != Guid.Empty)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();
        }

        private async Task<Guid?> GetClaimTenantIdAsync()
        {
            var tenants = await GetAvailableTenantsAsync();
            return tenants.FirstOrDefault()?.Id;
        }

        private static void AddTenantFromJsonElement(List<TenantSelectionDto> tenants, JsonElement element, string? fallbackName = null)
        {
            if (!element.TryGetProperty("id", out var idProperty))
            {
                return;
            }

            var idString = idProperty.GetString();
            if (!Guid.TryParse(idString, out var tenantId))
            {
                return;
            }

            var name = GetStringProperty(element, "name")
                ?? GetStringProperty(element, "displayName")
                ?? GetStringProperty(element, "tenantName")
                ?? GetStringProperty(element, "label")
                ?? fallbackName
                ?? tenantId.ToString();

            tenants.Add(new TenantSelectionDto
            {
                Id = tenantId,
                Name = name
            });
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private async Task<IEnumerable<TenantSelectionDto>> GetTenantsFromApiAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync("api/tenant/getall");
                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return [];
                }

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                var result = new List<TenantSelectionDto>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var id = GetStringProperty(item, "id") ?? GetStringProperty(item, "Id");
                    if (!Guid.TryParse(id, out var tenantId))
                    {
                        continue;
                    }

                    var name = GetStringProperty(item, "name")
                        ?? GetStringProperty(item, "Name")
                        ?? tenantId.ToString();

                    result.Add(new TenantSelectionDto
                    {
                        Id = tenantId,
                        Name = name
                    });
                }

                return result;
            }
            catch
            {
                return [];
            }
        }

        public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<RoleDto>>("api/auth/roles/getall") ?? new();
        }
    }
}
