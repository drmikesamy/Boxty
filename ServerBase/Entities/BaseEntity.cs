namespace Boxty.ServerBase.Entities
{
    public class BaseEntity<T> : IEntity
    where T : class
    {
        public Guid Id { get; set; }
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string LastModifiedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public Guid TenantId { get; set; }
        public Guid SubjectId { get; set; }
        public Guid CreatedById { get; set; }
        public Guid ModifiedById { get; set; }
    }
}
