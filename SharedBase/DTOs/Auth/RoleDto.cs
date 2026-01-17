using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;

namespace Boxty.SharedBase.DTOs.Auth
{
    public class RoleDto : IDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<PermissionDto> Permissions { get; set; } = new List<PermissionDto>();
    }
}
