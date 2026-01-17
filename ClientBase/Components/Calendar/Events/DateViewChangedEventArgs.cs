namespace Boxty.ClientBase.Components.Calendar.Events
{
    public class DateViewChangedEventArgs
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateViewChangedEventArgs(DateTime fromDate, DateTime toDate)
        {
            FromDate = fromDate;
            ToDate = toDate;
        }
    }
}
