using Boxty.SharedBase.Interfaces;

namespace Boxty.SharedBase.DTOs.UserManagement
{
    public class SubjectDto : BaseDto<SubjectDto>, IDto, IAuditDto, IAutoCrud, ISubject, IDateOfBirth
    {
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? DOB { get; set; }
        public string Address1 { get; set; } = string.Empty;
        public string? Address2 { get; set; }
        public string? Address3 { get; set; }
        public string Postcode { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;
        public Guid AvatarImageGuid { get; set; }
        public string AvatarImage { get; set; } = string.Empty;
        public string AvatarTitle { get; set; } = string.Empty;
        public string AvatarDescription { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public List<Guid> RelatedDocumentIds { get; set; } = new List<Guid>();
        public string DisplayName => $"{FirstName} {LastName}";
        public string? RoleName { get; set; }
    }
}