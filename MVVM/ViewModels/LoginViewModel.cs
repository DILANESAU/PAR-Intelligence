using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WPF_PAR.Converters;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;
using WPF_PAR.MVVM.Views;
using WPF_PAR.Services; // Para acceder a la vista del Pop-up

namespace WPF_PAR.MVVM.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;

        public ISnackbarMessageQueue MessageQueue { get; } = new SnackbarMessageQueue(System.TimeSpan.FromSeconds(3));

        // Propiedades
        private string _username;
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); ErrorMessage = string.Empty; }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // Comandos
        public RelayCommand LoginCommand { get; set; }
        public RelayCommand ExitCommand { get; set; }

        // CONSTRUCTOR SÚPER LIMPIO
        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
            LoginCommand = new RelayCommand(async param => await EjecutarLogin(param));
            ExitCommand = new RelayCommand(o => Application.Current.Shutdown());
        }

        // EL MÉTODO MAESTRO
        private async Task EjecutarLogin(object param)
        {
            if (IsBusy) return;

            var passwordBox = param as PasswordBox;
            var password = passwordBox?.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Ingresa usuario y contraseña.";
                return;
            }
            IsBusy = true;
            ErrorMessage = string.Empty;
            await Task.Delay(800); // Delay estético

            try
            {
                // 1. LLAMADA A BD (Asegúrate de que este método ya use el PasswordHasher para verificar)
                var usuarioEncontrado = await _authService.ValidarLoginAsync(Username, password);
                IsBusy = false;

                if (usuarioEncontrado != null)
                {
                    // 2. VERIFICACIÓN DE SEGURIDAD (¿Es contraseña temporal?)
                    if (usuarioEncontrado.RequiereCambioPwd)
                    {
                        // Levantamos el Pop-up de cambio de contraseña
                        var modalView = new CambiarPasswordView
                        {
                            DataContext = new CambiarPasswordViewModel(usuarioEncontrado.Username)
                        };

                        // Esperamos a ver si el usuario logra cambiar la clave
                        var resultado = await DialogHost.Show(modalView, "LoginRootDialog");

                        // 🛑 LA MAGIA ANTI-CUELGUES: 
                        // Le damos 300 milisegundos a WPF para que termine la animación de cierre del DialogHost
                        // antes de destruir la ventana de Login.
                        await Task.Delay(300);

                        // Si canceló la ventana o falló, no lo dejamos entrar
                        if (resultado is bool exito && exito)
                        {
                            // Actualizamos el modelo en memoria para que ya no pida el cambio
                            usuarioEncontrado.RequiereCambioPwd = false;
                            EntrarAlSistema(usuarioEncontrado);
                        }
                        else
                        {
                            ErrorMessage = "Cambio de contraseña cancelado. Acceso denegado.";
                            return;
                        }
                    }
                    else
                    {
                        // Si no requiere cambio, entra directo
                        EntrarAlSistema(usuarioEncontrado);
                    }
                }
                else
                {
                    ErrorMessage = "Credenciales incorrectas.";
                }
            }
            catch (System.Exception ex)
            {
                IsBusy = false;
                ErrorMessage = $"Error de conexión: {ex.Message}";
            }
        }

        // MÉTODO PARA ABRIR LA APP (Separado para no repetir código)
        private void EntrarAlSistema(UsuarioModel usuarioValido)
        {
            Converters.Session.UsuarioActual = usuarioValido;

            if (Application.Current is App app)
            {
                app.AbrirMainWindow(); // <--- Abre el Dashboard
            }

            // Cerrar ventana actual (Login)
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
    }
}