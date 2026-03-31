using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs.UserManagement;

namespace Boxty.ServerBase.Queries
{
    public interface IGetTenantByIdQuery
    {
        Task<TenantDto?> Handle(Guid tenantId);
    }

    public class GetTenantByIdQuery : IGetTenantByIdQuery, IQuery
    {
        private readonly IKeycloakService _keycloakService;

        public GetTenantByIdQuery(IKeycloakService keycloakService)
        {
            _keycloakService = keycloakService;
        }

        public async Task<TenantDto?> Handle(Guid tenantId)
        {
            var org = await _keycloakService.GetOrganizationByIdAsync(tenantId.ToString());
            if (org == null)
            {
                return null;
            }

            return new TenantDto
            {
                Id = Guid.Parse(org.Id ?? tenantId.ToString()),
                Name = org.Name ?? string.Empty,
                Domain = org.Domains?.FirstOrDefault()?.Name ?? string.Empty,
                IsActive = org.Enabled ?? true
            };
        }
    }
}