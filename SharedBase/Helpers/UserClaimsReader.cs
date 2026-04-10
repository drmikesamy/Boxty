using System.Security.Claims;
using System.Text.Json;
using Boxty.SharedBase.Interfaces;
using Boxty.SharedBase.Models;

namespace Boxty.SharedBase.Helpers
{
    public class UserClaimsReader : IUserClaimsReader
    {
        public string GetSubjectId(ClaimsPrincipal user)
        {
            return user.FindFirst("sub")?.Value ?? string.Empty;
        }

        public string GetOrganizationId(ClaimsPrincipal user)
        {
            return GetOrganizations(user).FirstOrDefault()?.Id.ToString() ?? string.Empty;
        }

        public string GetFullName(ClaimsPrincipal user)
        {
            var firstName = user.FindFirst("given_name")?.Value ?? string.Empty;
            var lastName = user.FindFirst("family_name")?.Value ?? string.Empty;
            return $"{firstName} {lastName}".Trim();
        }

        public List<string> GetRoles(ClaimsPrincipal user)
        {
            if (user == null)
            {
                return [];
            }

            var roles = new HashSet<string>(user.FindAll("role").Select(claim => claim.Value), StringComparer.OrdinalIgnoreCase);

            AddRolesFromClaim(user, roles, "realm_access", root =>
            {
                return root.TryGetProperty("roles", out var realmRoles) && realmRoles.ValueKind == JsonValueKind.Array
                    ? realmRoles.EnumerateArray().Select(role => role.GetString())
                    : Enumerable.Empty<string?>();
            });

            AddRolesFromClaim(user, roles, "resource_access", root =>
            {
                return root.ValueKind != JsonValueKind.Object
                    ? Enumerable.Empty<string?>()
                    : root.EnumerateObject()
                        .Where(resource => resource.Value.TryGetProperty("roles", out var resourceRoles) && resourceRoles.ValueKind == JsonValueKind.Array)
                        .SelectMany(resource => resource.Value.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
            });

            return roles.ToList();
        }

        public IReadOnlyList<UserOrganizationInfo> GetOrganizations(ClaimsPrincipal user)
        {
            var organizations = new List<UserOrganizationInfo>();
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

        private static void AddOrganization(List<UserOrganizationInfo> organizations, JsonElement element, string? fallbackName = null)
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

            organizations.Add(new UserOrganizationInfo
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
    }
}