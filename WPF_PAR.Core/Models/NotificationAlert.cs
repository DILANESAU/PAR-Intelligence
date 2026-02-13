using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public enum AlertType
    {
        Success,
        Error,
        Info,
        Warning
    }
    class NotificationAlert
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public AlertType Type { get; set; }
    }
}
