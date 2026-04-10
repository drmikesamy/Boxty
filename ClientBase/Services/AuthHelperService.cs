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
            if (user == null)
            {
                return null;
            }

            var firstName = user.FindFirst("given_name")?.Value ?? string.Empty;
            var lastName = user.FindFirst("family_name")?.Value ?? string.Empty;
            var fullName = $"{firstName} {lastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? user.FindFirst("name")?.Value : fullName;
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

            var roles = GetRoles(user);
            return roles.Contains("administrator", StringComparer.OrdinalIgnoreCase)
                || roles.Contains("tenantadministrator", StringComparer.OrdinalIgnoreCase);
        }

        public async Task<bool> IsUserInRole(string role)
        {
            var user = await GetUserAsync();
            if (user == null) return false;

            return GetRoles(user).Contains(role, StringComparer.OrdinalIgnoreCase);
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
            if (user == null)
            {
                return null;
            }

            var claimTenantId = GetOrganizations(user).FirstOrDefault()?.Id.ToString();
            if (Guid.TryParse(claimTenantId, out var tenantId))
            {
                return tenantId;
            }

            return null;
        }

        public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<RoleDto>>("api/auth/roles/getall") ?? new();
        }

        private static List<string> GetRoles(ClaimsPrincipal user)
        {
            var roles = new HashSet<string>(user.FindAll("role").Select(claim => claim.Value), StringComparer.OrdinalIgnoreCase);

            AddRolesFromClaim(user, roles, "realm_access", root =>
                root.TryGetProperty("roles", out var realmRoles) && realmRoles.ValueKind == JsonValueKind.Array
                    ? realmRoles.EnumerateArray().Select(role => role.GetString())
                    : Enumerable.Empty<string?>());

            AddRolesFromClaim(user, roles, "resource_access", root =>
                root.ValueKind != JsonValueKind.Object
                    ? Enumerable.Empty<string?>()
                    : root.EnumerateObject()
                        .Where(resource => resource.Value.TryGetProperty("roles", out var resourceRoles) && resourceRoles.ValueKind == JsonValueKind.Array)
                        .SelectMany(resource => resource.Value.GetProperty("roles").EnumerateArray().Select(role => role.GetString())));

            return roles.ToList();
        }

        private static IReadOnlyList<OrganizationInfo> GetOrganizations(ClaimsPrincipal user)
        {
            var organizations = new List<OrganizationInfo>();
            var organizationClaim = user.FindFirst("organization")?.Value;

            if (string.IsNullOrWhiteSpace(organizationClaim))
            {
                return organizations;
            }

            try
            {
                using var document = JsonDocument.Parse(organizationClaim);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        AddOrganization(organizations, item);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("id", out _))
                    {
                        AddOrganization(organizations, root);
                    }
                    else
                    {
                        foreach (var property in root.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.Object)
                            {
                                AddOrganization(organizations, property.Value, property.Name);
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return [];
            }

            return organizations
                .Where(organization => organization.Id != Guid.Empty)
                .GroupBy(organization => organization.Id)
                .Select(group => group.First())
                .ToList();
        }

        private static void AddRolesFromClaim(
            ClaimsPrincipal user,
            ISet<string> roles,
            string claimType,
            Func<JsonElement, IEnumerable<string?>> extractor)
        {
            var claimValue = user.FindFirst(claimType)?.Value;
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(claimValue);
                foreach (var role in extractor(document.RootElement))
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        roles.Add(role);
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        private static void AddOrganization(List<OrganizationInfo> organizations, JsonElement element, string? fallbackName = null)
        {
            if (!element.TryGetProperty("id", out var idProperty))
            {
                return;
            }

            var idValue = idProperty.GetString();
            if (!Guid.TryParse(idValue, out var organizationId))
            {
                return;
            }

            var name = GetStringProperty(element, "name")
                ?? GetStringProperty(element, "displayName")
                ?? GetStringProperty(element, "tenantName")
                ?? GetStringProperty(element, "label")
                ?? fallbackName
                ?? organizationId.ToString();

            organizations.Add(new OrganizationInfo
            {
                Id = organizationId,
                Name = name
            });
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
        }

        private sealed class OrganizationInfo
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
        }
    }
}
