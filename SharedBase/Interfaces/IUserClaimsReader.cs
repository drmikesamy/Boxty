using System.Security.Claims;
using Boxty.SharedBase.Models;

namespace Boxty.SharedBase.Interfaces
{
    public interface IUserClaimsReader
    {
        string GetSubjectId(ClaimsPrincipal user);
        string GetOrganizationId(ClaimsPrincipal user);
        string GetFullName(ClaimsPrincipal user);
        List<string> GetRoles(ClaimsPrincipal user);
        IReadOnlyList<UserOrganizationInfo> GetOrganizations(ClaimsPrincipal user);
    }
}