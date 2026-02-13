using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Services.Interfaces
{
    public interface IDialogService
    {
        void ShowMessage(string message, string title);
        void ShowError(string message, string title);
        string ShowSaveFileDialog(string filter, string defaultFileName);
        bool ShowConfirmation (string message, string title);
    }
}
