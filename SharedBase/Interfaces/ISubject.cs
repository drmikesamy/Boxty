using Boxty.SharedBase.DTOs;

namespace Boxty.SharedBase.Interfaces
{
    public interface ISubject : IDto
    {
        string Username { get; set; }
        string Email { get; set; }
        string FirstName { get; set; }
        string LastName { get; set; }
        Guid AvatarImageGuid { get; set; }
        string AvatarTitle { get; set; }
        string AvatarDescription { get; set; }
        string? RoleName { get; set; }
    }

}
