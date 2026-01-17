using Boxty.SharedBase.Interfaces;
using Heron.MudCalendar;

namespace Boxty.SharedBase.Models
{
    public class CalendarResource<TCalendarItem> : ICalendarResource<TCalendarItem>
        where TCalendarItem : BaseCalendarItem
    {
        public Guid AvatarImageGuid { get; set; } = Guid.Empty;
        public string AvatarImage { get; set; } = string.Empty;
        public string AvatarTitle { get; set; } = string.Empty;
        public string AvatarDescription { get; set; } = string.Empty;
        public IEnumerable<TCalendarItem> CalendarItems { get; set; } = new List<TCalendarItem>();
    }
}
