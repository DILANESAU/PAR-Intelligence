using MaterialDesignThemes.Wpf;

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using WPF_PAR.MVVM.Models;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.Services
{
    public class NotificationService : INotificationService
    {
        public SnackbarMessageQueue MessageQueue { get; }

        public NotificationService(IDialogService dialogService)
        {
            MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        }

        // --- MÉTODO PRIVADO PARA ENVIAR CON SEGURIDAD ---
        private void EnqueueSafely(NotificationAlert alerta)
        {
            // Esto asegura que SIEMPRE se ejecute en el hilo principal (UI)
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
            EnqueueSafely(alerta); // Usamos el método seguro
        }

        public void ShowError(string message)
        {
            var alerta = new NotificationAlert
            {
                Title = "Error",
                Message = message,
                Type = AlertType.Error
            };

            // Los errores a veces necesitan duración personalizada, 
            // pero para simplificar, usaremos la cola estándar con el objeto.
            // Si quieres duración extra, tendrías que ajustar el objeto o la cola.
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

        // ... El resto de tu código (ShowErrorDialog) está bien ...
        public async Task ShowErrorDialog(string message)
        {
            // ... tu código existente ...
            // Solo asegúrate de envolver el DialogHost.Show en Dispatcher si te da problemas
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // Tu lógica de creación de vista y DialogHost.Show aquí
                // ...
            });
        }
    }
}