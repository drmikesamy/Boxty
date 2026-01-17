using System;

namespace Boxty.ServerBase.Services.Interfaces
{
    public interface IRole
    {
        Guid Id { get; set; }
        string Name { get; set; }
        ICollection<IPermission> Permissions { get; set; }
    }
}
