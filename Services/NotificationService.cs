using MaterialDesignThemes.Wpf;

using System;
using System.Threading.Tasks;
using System.Windows; // Para Application.Current
using System.Windows.Controls;
using System.Windows.Media;

using WPF_PAR.Core.Models;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.Services
{
    public class NotificationService : INotificationService
    {
        public SnackbarMessageQueue MessageQueue { get; }
        private readonly IDialogService _dialogService;

        public NotificationService() : this(new DialogService())
        {
        }
        public NotificationService(IDialogService dialogService)
        {
            _dialogService = dialogService;
            MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        }
        private void EnqueueSafely(NotificationAlert alerta)
        {
            if ( Application.Current == null ) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageQueue.Enqueue(alerta);
            });
        }
        public void ShowSuccess(string message)
        {
            var alerta = new NotificationAlert
            {
                Title = "Éxito",
                Message = message,
                Type = AlertType.Success
            };
            EnqueueSafely(alerta);
        }
        public void ShowError(string message)
        {
            var alerta = new NotificationAlert
            {
                Title = "Error",
                Message = message,
                Type = AlertType.Error
            };
            EnqueueSafely(alerta);
        }
        public void ShowInfo(string message)
        {
            var alerta = new NotificationAlert
            {
                Title = "Información",
                Message = message,
                Type = AlertType.Info
            };
            EnqueueSafely(alerta);
        }
        public async Task ShowErrorDialog(string message)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if ( _dialogService != null )
                {
                    _dialogService.ShowMessage("Error Crítico", message);
                }
            });
        }
    }
}