using Boxty.SharedBase.Interfaces;
using Boxty.SharedBase.DTOs;
using Heron.MudCalendar;
namespace Boxty.SharedBase.Models
{
    public class BaseCalendarItem : CalendarItem, IDto, IAuditDto, IAutoCrud
    {
        public DateTime? StartTime
        {
            get
            {
                return Start;
            }
            set
            {
                if (value != null)
                {
                    Start = value.Value;
                }
                else
                {
                    Start = DateTime.UtcNow;
                    StartTime = Start;
                }
            }
        }
        public DateTime? EndTime
        {
            get
            {
                return End;
            }
            set
            {
                End = value;
            }
        }
        public MudBlazor.Color Color { get; set; } = MudBlazor.Color.Tertiary;
        public MudBlazor.Color ChipColor { get; set; } = MudBlazor.Color.Secondary;
        public string? ChipText { get; set; }
        public string? Heading { get; set; }
        public string? Body { get; set; }
        public Guid Id { get; set; } = Guid.Empty;
        public virtual bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = string.Empty;
        public string LastModifiedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public Guid SubjectId { get; set; } = Guid.Empty;
        public Guid TenantId { get; set; } = Guid.Empty;
        public string DisplayName => Heading ?? string.Empty;
    }
}

