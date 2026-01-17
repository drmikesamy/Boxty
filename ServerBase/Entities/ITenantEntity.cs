using Boxty.ServerBase.Entities;

namespace Boxty.SharedBase.Interfaces
{
    public interface ITenantEntity
    {
        string Name { get; set; }
        string Domain { get; set; }
    }
}
