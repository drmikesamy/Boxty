namespace Boxty.SharedBase.Interfaces
{
    public interface ICalendarResource<TCalendarItem>
    where TCalendarItem : Heron.MudCalendar.CalendarItem
    {
        IEnumerable<TCalendarItem> CalendarItems { get; set; }
    }
}
