using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using WPF_PAR.Core;
using WPF_PAR.Services;

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

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;

            LoginCommand = new RelayCommand(async param =>
            {
                // 1. VALIDACIÓN PRIMERO (Antes de llamar a la BD)
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

                // Delay estético
                await Task.Delay(1000);

                try
                {
                    // 3. LLAMADA A BD (Ahora sí, dentro del Try-Catch)
                    var usuarioEncontrado = await _authService.ValidarLoginAsync(Username, password);

                    IsBusy = false;

                    if ( usuarioEncontrado != null )
                    {
                        // 4. ¡CRUCIAL! GUARDAR EN SESIÓN
                        // Sin esto, el MainViewModel explota
                        Session.UsuarioActual = usuarioEncontrado;

                        if ( Application.Current is App app )
                        {
                            app.AbrirMainWindow();

                            foreach ( Window window in Application.Current.Windows )
                            {
                                if ( window is Views.LoginWindow )
                                {
                                    window.Close();
                                    break;
                                }
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