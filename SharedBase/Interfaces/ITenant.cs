using Boxty.SharedBase.DTOs;

namespace Boxty.SharedBase.Interfaces
{
    public interface ITenant : IDto
    {
        string Name { get; set; }
        string Domain { get; set; }
    }
}
