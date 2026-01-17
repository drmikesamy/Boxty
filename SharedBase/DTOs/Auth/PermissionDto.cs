using Boxty.SharedBase.DTOs;

namespace Boxty.SharedBase.DTOs.Auth
{
    public class PermissionDto : IDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
