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

        public static string TimeAgo(DateTime d)
        {
            var diff = DateTime.Now - d;
            if (diff.TotalMinutes >= 0 && diff.TotalHours < 24)
                return diff.TotalHours < 1 ? (int)diff.TotalMinutes + " minutes ago" : (int)diff.TotalHours + " hours ago";
            if (d.Date == DateTime.Today.AddDays(-1)) return "Yesterday, " + d.ToString("h:mmtt");
            return Short(d);
        }
    }
}
