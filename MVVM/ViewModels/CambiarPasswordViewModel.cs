using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Windows.Controls;
using WPF_PAR.Converters;
using WPF_PAR.Core.Services;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class CambiarPasswordViewModel : ObservableObject
    {
        private readonly INotificationService _notificationService;
        //public string Usernam { get; }

        public string UsuarioLogin { get; }
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand GuardarNuevaPasswordCommand { get; set; }
        public CambiarPasswordViewModel(string usuarioLogin)
        {
            UsuarioLogin = usuarioLogin;

            _notificationService = new NotificationService();
            GuardarNuevaPasswordCommand = new RelayCommand(param => GuardarPassword(param));
        }

        private async void GuardarPassword(object parameter)
        {
            var passwordBoxes = parameter as object[];
            if (passwordBoxes == null || passwordBoxes.Length < 2)
            {
                System.Windows.MessageBox.Show("Error de UI: Las contraseñas no llegaron al código.\nRevisa el ArrayConverter en el XAML.", "Fallo Silencioso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var txtNueva = passwordBoxes[0] as PasswordBox;
            var txtConfirmacion = passwordBoxes[1] as PasswordBox;

            string nueva = txtNueva?.Password;
            string confirmacion = txtConfirmacion?.Password;

            if (txtNueva == null || txtConfirmacion == null)
            {
                System.Windows.MessageBox.Show("Error de UI: Los controles no son reconocidos como PasswordBox.", "Fallo Silencioso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            // --- Validaciones Normales ---
            if (string.IsNullOrWhiteSpace(nueva) || string.IsNullOrWhiteSpace(confirmacion))
            {
                System.Windows.MessageBox.Show("Por favor, llena ambos campos.", "Aviso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (nueva != confirmacion)
            {
                System.Windows.MessageBox.Show("Las contraseñas no coinciden. Intenta de nuevo.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            if (nueva.Length < 6)
            {
                System.Windows.MessageBox.Show("La contraseña debe tener al menos 6 caracteres por seguridad.", "Seguridad", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 👇 ENCENDEMOS EL SPINNER
            IsBusy = true;

            try
            {
                string connString = System.Configuration.ConfigurationManager.ConnectionStrings["ParSystem"]?.ConnectionString;
                var sqlHelper = new WPF_PAR.Core.Services.SqlHelper(connString);

                string passwordEncriptado = WPF_PAR.Services.PasswordHasher.HashPassword(nueva);

                // ⚠️ Asegúrate de que el campo RequiereCambioPwd se llame exactamente igual que en Navicat
                string query = @"
                    UPDATE Usuarios 
                    SET PasswordHash = @NuevoPassword, RequiereCambioPwd = 0 
                    WHERE Username = @Username";

                // 👇 CORRECCIÓN 1: Usamos this.Username (la propiedad de tu clase), NO parameter
                int filasAfectadas = await sqlHelper.ExecuteAsync(query, new
                {
                    NuevoPassword = passwordEncriptado,
                    Username = this.UsuarioLogin
                });

                if (filasAfectadas > 0)
                {
                    // 👇 AQUÍ USAMOS EL MESSAGEBOX EN LUGAR DEL SNACKBAR
                    System.Windows.MessageBox.Show("¡Contraseña actualizada con éxito!\nAhora ingresarás al sistema.", "Seguridad", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                    // 👇 CORRECCIÓN 2: Forzamos el cierre comunicándonos directamente con el DialogHost
                    MaterialDesignThemes.Wpf.DialogHost.Close("LoginRootDialog", true);
                }
                else
                {
                    System.Windows.MessageBox.Show($"Error interno: No se pudo localizar al usuario '{this.UsuarioLogin}' en la base de datos.", "Error SQL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error al actualizar la contraseña:\n" + ex.Message, "Error Crítico", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                // 👇 CORRECCIÓN 3: Apagamos el spinner SIEMPRE
                IsBusy = false;
            }
        }
    }
}
