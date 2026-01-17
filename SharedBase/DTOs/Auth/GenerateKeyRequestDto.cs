namespace Boxty.SharedBase.DTOs.Auth
{
    public class GenerateKeyRequestDto
    {
        public required string ObjectTypeName { get; set; }
        public required Guid ObjectGuid { get; set; }
    }
}
