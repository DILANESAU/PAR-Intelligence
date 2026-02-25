using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

// Usings necesarios
using WPF_PAR.Converters;
using WPF_PAR.Services;      // Si AuthService está aquí
// using WPF_PAR.Core.Services; // O aquí, revisa dónde vive AuthService

namespace WPF_PAR.MVVM.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;

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

        // CONSTRUCTOR ACTUALIZADO: Recibe string connectionString
        public LoginViewModel(string connectionString)
        {
            // Creamos el servicio de autenticación con la conexión
            _authService = new AuthService(connectionString);

            LoginCommand = new RelayCommand(async param =>
            {
                // 1. VALIDACIÓN
                if ( IsBusy ) return;

                var passwordBox = param as PasswordBox;
                var password = passwordBox?.Password;

                if ( string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password) )
                {
                    ErrorMessage = "Ingresa usuario y contraseña.";
                    return;
                }

                // 2. ACTIVAR SPINNER
                IsBusy = true;
                ErrorMessage = string.Empty;

                await Task.Delay(1000); // Delay estético

                try
                {
                    // 3. LLAMADA A BD
                    var usuarioEncontrado = await _authService.ValidarLoginAsync(Username, password);

                    IsBusy = false;

                    if ( usuarioEncontrado != null )
                    {
                        // 4. GUARDAR EN SESIÓN
                        Session.UsuarioActual = usuarioEncontrado;

                        if ( Application.Current is App app )
                        {
                            app.AbrirMainWindow(); // <--- Asegúrate de tener este método en App.xaml.cs
                        }

                        // Cerrar ventana actual (Login)
                        foreach ( Window window in Application.Current.Windows )
                        {
                            if ( window.DataContext == this )
                            {
                                window.Close();
                                break;
                            }
                        }
                    }
                    else
                    {
                        ErrorMessage = "Credenciales incorrectas.";
                    }
                }
                catch ( System.Exception ex )
                {
                    IsBusy = false;
                    ErrorMessage = $"Error de conexión: {ex.Message}";
                }
            });

            ExitCommand = new RelayCommand(o => Application.Current.Shutdown());
        }
    }
}