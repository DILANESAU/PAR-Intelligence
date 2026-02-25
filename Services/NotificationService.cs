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

        // 1. CONSTRUCTOR VACÍO (Compatibilidad con ClientesViewModel)
        // Si no le pasamos nada, él crea su propio DialogService.
        public NotificationService() : this(new DialogService())
        {
        }

        // 2. CONSTRUCTOR CON INYECCIÓN (El ideal)
        public NotificationService(IDialogService dialogService)
        {
            _dialogService = dialogService;
            // Configuración del tiempo del Snackbar (3 segundos)
            MessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        }

        // --- MÉTODO PRIVADO PARA ENVIAR CON SEGURIDAD (HILO UI) ---
        private void EnqueueSafely(NotificationAlert alerta)
        {
            if ( Application.Current == null ) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Aquí encolamos el OBJETO alerta. 
                // Asegúrate de que en tu XAML del MainWindow el Snackbar MessageTemplate 
                // sepa cómo mostrar un objeto 'NotificationAlert', o si no, pásale solo el string.

                // Opción A: Si tu XAML soporta el objeto complejo:
                MessageQueue.Enqueue(alerta);

                // Opción B (Más segura si no tienes DataTemplates complejos):
                // MessageQueue.Enqueue(alerta.Message, "OK", () => { });
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
                // Usamos el servicio de diálogo que inyectamos o creamos
                if ( _dialogService != null )
                {
                    // Asumiendo que tu DialogService tiene un método ShowMessage o similar
                    // Si tu interfaz IDialogService tiene ShowError, úsalo.
                    // Aquí llamamos a un método genérico de mensaje como ejemplo:
                    _dialogService.ShowMessage("Error Crítico", message);
                }
            });
        }
    }
}