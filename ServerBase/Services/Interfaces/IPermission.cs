using System;

namespace Boxty.ServerBase.Services.Interfaces
{
    public interface IPermission
    {
        Guid Id { get; set; }
        string Name { get; set; }
    }
}
