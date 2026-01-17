namespace Boxty.SharedBase.DTOs
{
    public class BaseDto<T> : IAuditDto
    where T : class
    {
        public Guid Id { get; set; } = Guid.Empty;
        public virtual bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = string.Empty;
        public string LastModifiedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public Guid SubjectId { get; set; } = Guid.Empty;
        public Guid TenantId { get; set; } = Guid.Empty;
    }
}
