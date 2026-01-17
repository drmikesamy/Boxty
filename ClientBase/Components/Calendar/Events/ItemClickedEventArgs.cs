using System;
using Boxty.SharedBase.Models;

namespace Boxty.ClientBase.Components.Calendar.Events
{
    public class ItemClickedEventArgs<TCalendarItem>
        where TCalendarItem : BaseCalendarItem
    {
        public TCalendarItem Item { get; set; }

        public ItemClickedEventArgs(TCalendarItem item)
        {
            Item = item;
        }
    }
}
