namespace Boxty.SharedBase.Helpers
{
    public static class StringExtensions
    {
        public static bool ContainsIgnoreCase(this string input, string substring)
        {
            return input?.IndexOf(substring, StringComparison.OrdinalIgnoreCase) > -1;
        }
    }
}
