using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.Services
{
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        public bool ShowConfirmation(string message, string title)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public string ShowSaveFileDialog(string filter, string defaultFileName)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName
            };

            if ( saveFileDialog.ShowDialog() == true )
            {
                return saveFileDialog.FileName;
            }
            return null;
        }
    }
}
