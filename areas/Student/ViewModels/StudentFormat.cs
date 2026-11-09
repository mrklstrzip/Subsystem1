using System;
using System.Text.RegularExpressions;
using System.Web;

namespace Subsystem1.Areas.Student.ViewModels
{
    /// <summary>Small display helpers used by the Student views.</summary>
    public static class StudentFormat
    {
        /// <summary>HTML-encodes the text, then turns **word** into bold.</summary>
        public static HtmlString Bold(string text)
        {
            return new HtmlString(Regex.Replace(HttpUtility.HtmlEncode(text ?? ""), @"\*\*(.+?)\*\*", "<b>$1</b>"));
        }

        public static string Short(DateTime d) { return (d.Month == 9 ? "Sept." : d.ToString("MMM.")) + " " + d.Day; }
        public static string Stamp(DateTime d) { return Short(d) + ", " + d.ToString("h:mmtt"); }

        // All stored times are Philippine clock time, so "now" is Philippine time too.
        public static DateTime Now() { return DateTime.UtcNow.AddHours(8); }

        public static string TimeAgo(DateTime d)
        {
            var now = Now();
            var diff = now - d;
            if (diff.TotalMinutes >= 0 && diff.TotalHours < 24)
            {
                if (diff.TotalMinutes < 1) return "Just now";
                return diff.TotalHours < 1
                    ? (int)diff.TotalMinutes + (((int)diff.TotalMinutes) == 1 ? " minute ago" : " minutes ago")
                    : (int)diff.TotalHours + (((int)diff.TotalHours) == 1 ? " hour ago" : " hours ago");
            }
            if (d.Date == now.Date.AddDays(-1)) return "Yesterday, " + d.ToString("h:mmtt");
            return Short(d);
        }
    }
}
