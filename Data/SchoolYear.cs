using System;

namespace Subsystem1.Data
{
    /// <summary>
    /// School year helper. One school year runs from June 1 to May 31, written "2026-2027".
    /// The same rule is used by Scripts/school-year-filter.js, so change both together.
    /// </summary>
    public static class SchoolYear
    {
        public const int StartMonth = 6;

        public static string Of(DateTime date)
        {
            int start = date.Month >= StartMonth ? date.Year : date.Year - 1;
            return start + "-" + (start + 1);
        }

        public static string Of(DateTime? date)
        {
            return date.HasValue ? Of(date.Value) : "";
        }

        public static string Current()
        {
            return Of(DateTime.UtcNow.AddHours(8));   // Philippine time
        }
    }
}
