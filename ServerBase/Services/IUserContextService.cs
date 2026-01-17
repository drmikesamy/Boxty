using System.Security.Claims;

namespace Boxty.ServerBase.Services
{
    /// <summary>
    /// Service for accessing user context information from HTTP context
    /// </summary>
    public interface IUserContextService
    {
        /// <summary>
        /// Gets the subject id ("sub" claim) from the JWT.
        /// </summary>
        string GetSubjectId(ClaimsPrincipal user);

        /// <summary>
        /// Gets the organization id from the custom "organization" claim.
        /// </summary>
        string GetOrganizationId(ClaimsPrincipal user);

        /// <summary>
        /// Gets the user's full name from the claims.
        /// </summary>
        string GetFullName(ClaimsPrincipal user);

        /// <summary>
        /// Gets all roles from the user's claims, including realm_access and resource_access.
        /// </summary>
        List<string> GetRoles(ClaimsPrincipal user);

        /// <summary>
        /// Gets the current user's ClaimsPrincipal
        /// </summary>
        ClaimsPrincipal? GetUser(ClaimsPrincipal user);
    }
}
