namespace Boxty.SharedBase.Interfaces
{
    public interface ICalendarItem
    {
        string Id { get; set; }
        string Title { get; set; }
        string Description { get; set; }
        DateTime Start { get; set; }
        DateTime End { get; set; }
    }
}
