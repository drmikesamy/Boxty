namespace Boxty.ServerBase.Entities
{
    public interface ISubjectEntity
    {
        string Username { get; set; }
        string Email { get; set; }
        string FirstName { get; set; }
        string LastName { get; set; }
        string? RoleName { get; set; }
    }

}
