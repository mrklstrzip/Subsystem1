using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Dean.Models
{
    public class NotificationModels
    {}
    public class NotificationItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string TimeAgo { get; set; }
        public bool IsUnread { get; set; }
        public bool Today { get; set; }
    }

    public class NotificationPanelViewModel
    {
        public List<NotificationItem> Items { get; set; } = new List<NotificationItem>();
        public int UnreadCount => Items.Count(x => x.IsUnread);
    }
}