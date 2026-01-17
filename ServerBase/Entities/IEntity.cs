namespace Boxty.ServerBase.Entities
{
    public interface IEntity
    {
        Guid Id { get; set; }
        bool IsActive { get; set; }
        string CreatedBy { get; set; }
        string LastModifiedBy { get; set; }
        DateTime CreatedDate { get; set; }
        DateTime ModifiedDate { get; set; }
        Guid SubjectId { get; set; }
        Guid TenantId { get; set; }
        Guid CreatedById { get; set; }
        Guid ModifiedById { get; set; }
    }
}
