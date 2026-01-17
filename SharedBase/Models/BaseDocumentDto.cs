using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;

namespace Boxty.SharedBase.Models
{
    public class BaseDocumentDto : BaseDto<BaseDocumentDto>, IDto, IAuditDto, IDocumentDto, IAutoCrud
    {
        public string BlobName { get; set; } = string.Empty;
        public string BlobContainerName { get; set; } = string.Empty;
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string DisplayName => $"{Name} - {Description ?? "No description"}";
    }
}
