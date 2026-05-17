using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;

namespace Boxty.SharedBase.DTOs.Auth
{
    public class PermissionDto : IDto, IAutoCrud
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName => Name;
    }
}
