using MaterialDesignThemes.Wpf;

using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Services.Interfaces
{
    public interface INotificationService
    {
        SnackbarMessageQueue MessageQueue { get; }

        void ShowSuccess(string message);
        void ShowError(string message);
        void ShowInfo(string message);
        Task ShowErrorDialog(string message);
    }
}
