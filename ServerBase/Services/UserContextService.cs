using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Boxty.ServerBase.Services
{
    /// <summary>
    /// Service for accessing user context information from HTTP context
    /// </summary>
    public class UserContextService : IUserContextService
    {
        /// <summary>
        /// Gets the subject id ("sub" claim) from the JWT.
        /// </summary>
        public string GetSubjectId(ClaimsPrincipal user)
        {
            return user.FindFirst("sub")?.Value ?? string.Empty;
        }

        /// <summary>
        /// Gets the organization id from the custom "organization" claim.
        /// </summary>
        public string GetOrganizationId(ClaimsPrincipal user)
        {
            var orgClaim = user.FindFirst("organization")?.Value;
            if (string.IsNullOrEmpty(orgClaim))
                return string.Empty;

            try
            {
                Console.WriteLine($"Parsing Organization Claim: {orgClaim}");
                using var doc = JsonDocument.Parse(orgClaim);
                var root = doc.RootElement;
                // Handle array format (existing logic)
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstObj = root[0];
                    if (firstObj.TryGetProperty("id", out var directIdProp))
                    {
                        return directIdProp.GetString() ?? string.Empty;
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
                            return nestedIdProp.GetString() ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
                // Ignore parse errors and return empty string
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the user's full name from the claims.
        /// </summary>
        public string GetFullName(ClaimsPrincipal user)
        {
            var firstName = user.FindFirst("given_name")?.Value ?? string.Empty;
            var lastName = user.FindFirst("family_name")?.Value ?? string.Empty;
            return $"{firstName} {lastName}".Trim();
        }

        /// <summary>
        /// Gets all roles from the user's claims, including realm_access and resource_access.
        /// </summary>
        public List<string> GetRoles(ClaimsPrincipal user)
        {
            var roles = new List<string>();

            if (user == null) return roles;

            // Get roles from standard role claims
            var roleClaims = user.FindAll("role").Select(c => c.Value);
            roles.AddRange(roleClaims);

            // Get roles from realm_access
            var realmAccessClaim = user.FindFirst("realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccessClaim))
            {
                try
                {
                    using var doc = JsonDocument.Parse(realmAccessClaim);
                    if (doc.RootElement.TryGetProperty("roles", out var realmRoles) &&
                        realmRoles.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var role in realmRoles.EnumerateArray())
                        {
                            var roleValue = role.GetString();
                            if (!string.IsNullOrEmpty(roleValue) && !roles.Contains(roleValue))
                            {
                                roles.Add(roleValue);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parse errors
                }
            }

            // Get roles from resource_access
            var resourceAccessClaim = user.FindFirst("resource_access")?.Value;
            if (!string.IsNullOrEmpty(resourceAccessClaim))
            {
                try
                {
                    using var doc = JsonDocument.Parse(resourceAccessClaim);
                    foreach (var resource in doc.RootElement.EnumerateObject())
                    {
                        if (resource.Value.TryGetProperty("roles", out var resourceRoles) &&
                            resourceRoles.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var role in resourceRoles.EnumerateArray())
                            {
                                var roleValue = role.GetString();
                                if (!string.IsNullOrEmpty(roleValue) && !roles.Contains(roleValue))
                                {
                                    roles.Add(roleValue);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parse errors
                }
            }

            return roles.Distinct().ToList();
        }

        /// <summary>
        /// Gets the current user's ClaimsPrincipal
        /// </summary>
        public ClaimsPrincipal? GetUser(ClaimsPrincipal user)
        {
            return user;
        }
    }
}
