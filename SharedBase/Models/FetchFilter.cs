namespace Boxty.SharedBase.Models
{
    public class FetchFilter
    {
        public bool? IsActive { get; set; } = true;
        public string? SearchTerm { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
