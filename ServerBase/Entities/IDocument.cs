namespace Boxty.ServerBase.Entities
{
    public interface IDocument
    {
        string BlobContainerName { get; set; }
        string BlobName { get; set; }
        string Name { get; set; }
        string? Description { get; set; }
    }
}
