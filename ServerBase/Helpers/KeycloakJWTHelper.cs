using System.Security.Claims;
using System.Text.Json;

namespace Boxty.ServerBase.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Extracts the user tenant id from the "organization" claim, if present.
        /// </summary>
        public static string GetUserTenantId(this ClaimsPrincipal user)
        {
            var orgClaim = user.FindFirst("organization")?.Value;
            if (string.IsNullOrEmpty(orgClaim))
                return string.Empty;

            using var doc = JsonDocument.Parse(orgClaim);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.TryGetProperty("id", out var idProp))
                    {
                        return idProp.GetString() ?? string.Empty;
                    }
                }
            }
            return string.Empty;
        }
    }
}
