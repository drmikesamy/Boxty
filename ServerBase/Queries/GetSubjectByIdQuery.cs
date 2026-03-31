using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs.UserManagement;

namespace Boxty.ServerBase.Queries
{
    public interface IGetSubjectByIdQuery
    {
        Task<SubjectDto?> Handle(Guid subjectId);
    }

    public class GetSubjectByIdQuery : IGetSubjectByIdQuery, IQuery
    {
        private readonly IKeycloakService _keycloakService;

        public GetSubjectByIdQuery(IKeycloakService keycloakService)
        {
            _keycloakService = keycloakService;
        }

        public async Task<SubjectDto?> Handle(Guid subjectId)
        {
            var subject = await _keycloakService.GetUserByIdAsync(subjectId.ToString());
            if (subject == null)
            {
                return null;
            }

            var userRoles = await _keycloakService.GetUserRolesAsync(subjectId.ToString());

            return new SubjectDto
            {
                Id = Guid.Parse(subject.Id ?? subjectId.ToString()),
                Username = subject.Username ?? string.Empty,
                FirstName = subject.FirstName ?? string.Empty,
                LastName = subject.LastName ?? string.Empty,
                Email = subject.Email ?? string.Empty,
                TenantId = Guid.Empty,
                RoleName = userRoles.FirstOrDefault()?.Name,
                IsActive = subject.Enabled ?? true
            };
        }
    }
}