using System.Globalization;

namespace Boxty.ClientBase.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToLocalDisplayString(this DateTime dateTime, string format = "dd-MM-yyyy HH:mm:ss", string emptyText = "")
        {
            if (dateTime <= DateTime.MinValue)
                return emptyText;
            return dateTime.ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
        }

        public static string ToLocalDisplayString(this DateTime? dateTime, string format = "dd-MM-yyyy HH:mm:ss", string emptyText = "")
        {
            if (dateTime <= DateTime.MinValue)
                return emptyText;
            return dateTime?.ToLocalTime().ToString(format, CultureInfo.CurrentCulture) ?? emptyText;
        }
    }
}