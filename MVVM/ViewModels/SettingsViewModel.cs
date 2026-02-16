using MaterialDesignThemes.Wpf;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls; // Para PasswordBox

using WPF_PAR.Converters;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

using static WPF_PAR.Services.SqlHelper;

namespace WPF_PAR.MVVM.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        // --- SERVICIOS ---
        private readonly SucursalesService _sucursalesService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService; // Agregado para toasts
        private readonly ThemeService _themeService;

        // --- SERVIDOR AUTH (Login) ---
        public string AuthServer { get; set; }
        public string AuthDb { get; set; }
        public string AuthUser { get; set; }

        // --- SERVIDOR DATA (Reportes) ---
        public string DataServer { get; set; }
        public string DataDb { get; set; }
        public string DataUser { get; set; }

        private bool _esModoTecnico;
        public bool EsModoTecnico
        {
            get => _esModoTecnico;
            set { _esModoTecnico = value; OnPropertyChanged(); }
        }

        // --- PROPIEDADES DE SUCURSAL ---
        private Dictionary<int, string> _misSucursales;
        public Dictionary<int, string> MisSucursales
        {
            get => _misSucursales;
            set
            {
                _misSucursales = value;
                OnPropertyChanged(); // Esto avisa al ComboBox que la lista ya llegó
            }
        }

        private int _miSucursalDefault;
        public int MiSucursalDefault
        {
            get => _miSucursalDefault;
            set { _miSucursalDefault = value; OnPropertyChanged(); }
        }

        // --- PROPIEDADES DE MODO OSCURO ---
        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if ( _isDarkMode != value )
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                    _themeService.SetThemeMode(_isDarkMode);

                    // Guardar preferencia inmediatamente
                    Properties.Settings.Default.IsDarkMode = value;
                    Properties.Settings.Default.Save();
                }
            }
        }

        // --- PROPIEDADES DE CONEXIÓN SQL ---
        private string _serverIP;
        public string ServerIP
        {
            get => _serverIP;
            set { _serverIP = value; OnPropertyChanged(); }
        }

        private string _databaseName;
        public string DatabaseName
        {
            get => _databaseName;
            set { _databaseName = value; OnPropertyChanged(); }
        }

        private string _dbUser;
        public string DbUser
        {
            get => _dbUser;
            set { _dbUser = value; OnPropertyChanged(); }
        }

        // --- COMANDOS ---
        public RelayCommand GuardarSucursalCommand { get; set; }
        public RelayCommand ProbarConexionCommand { get; set; }
        public RelayCommand GuardarConexionCommand { get; set; }
        public RelayCommand VerLogsCommand { get; set; }
        public RelayCommand ContactarSoporteCommand { get; set; }

        // =========================================================================
        // CONSTRUCTOR
        // =========================================================================
        public SettingsViewModel(IDialogService dialogService, INotificationService notificationService)
        {
            _dialogService = dialogService;
            _notificationService = notificationService;

            _themeService = new ThemeService();
            _sucursalesService = new SucursalesService();

            DeterminarPermisos();

            // 1. Cargar Modo Oscuro
            _isDarkMode = Properties.Settings.Default.IsDarkMode;

            // 2. Cargar Datos de Conexión guardados
            CargarDatosConexion();

            // 3. Cargar Sucursales
            CargarConfiguracionSucursales();

            // 4. Inicializar Comandos
            GuardarSucursalCommand = new RelayCommand(o => GuardarPreferenciaSucursal());


            AuthServer = Properties.Settings.Default.Auth_Server;
            AuthDb = Properties.Settings.Default.Auth_Db;
            AuthUser = Properties.Settings.Default.Auth_User;

            DataServer = Properties.Settings.Default.Data_Server;
            DataDb = Properties.Settings.Default.Data_Db;
            DataUser = Properties.Settings.Default.Data_User;

            // Los comandos de conexión reciben el PasswordBox como parámetro por seguridad
            ProbarConexionCommand = new RelayCommand(param => ProbarConexion(param));
            GuardarConexionCommand = new RelayCommand(param => GuardarConexion(param));

            VerLogsCommand = new RelayCommand(o => System.Diagnostics.Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory));
            ContactarSoporteCommand = new RelayCommand(o => _dialogService.ShowMessage("Soporte", "Envía un correo a sistemas@par.com"));
        }

        // =========================================================================
        // LÓGICA DE SUCURSALES
        // =========================================================================
        private void CargarConfiguracionSucursales()
        {
            try
            {
                var todas = _sucursalesService.CargarSucursales();

                // NUEVA LÓGICA: Si es Admin o Sistemas, ve TODO
                bool esSuperUsuario = Session.UsuarioActual?.Rol == "Director" ||
                                      Session.UsuarioActual?.Rol == "Sistemas" || Session.UsuarioActual?.Rol == "Admin";

                if ( esSuperUsuario || Session.UsuarioActual?.SucursalesPermitidas == null )
                {
                    MisSucursales = todas;
                }
                else
                {
                    MisSucursales = todas
                        .Where(s => Session.UsuarioActual.SucursalesPermitidas.Contains(s.Key))
                        .ToDictionary(k => k.Key, v => v.Value);
                }

                // 3. Si por alguna razón la lista quedó vacía (raro), metemos una de respaldo
                if ( MisSucursales.Count == 0 )
                {
                    MisSucursales.Add(0, "0 - TODAS (Sin restricciones)");
                }

                // 4. Cargamos la preferencia guardada
                int guardada = Properties.Settings.Default.SucursalDefaultId;

                if ( MisSucursales.ContainsKey(guardada) )
                {
                    MiSucursalDefault = guardada;
                }
                else
                {
                    // Si la guardada ya no existe o no tiene permiso, seleccionamos la primera disponible
                    MiSucursalDefault = MisSucursales.Keys.First();
                }

                // Notificamos a la vista que la lista cambió
                OnPropertyChanged(nameof(MisSucursales));
            }
            catch ( Exception ex )
            {
                System.Diagnostics.Debug.WriteLine("Error configurando sucursales en Settings: " + ex.Message);
                // Fallback total para que la pantalla no se quede en blanco
                MisSucursales = new Dictionary<int, string> { { 0, "0 - TODAS" } };
                MiSucursalDefault = 0;
                OnPropertyChanged(nameof(MisSucursales));
            }
        }

        private void DeterminarPermisos()
        {
            // REGLA 1: Si no hay usuario logueado (Primera vez o desde Login),
            // permitimos editar para poder conectar el sistema.
            if ( Session.UsuarioActual == null )
            {
                EsModoTecnico = true;
                return;
            }

            if ( Session.UsuarioActual.Rol.Equals("Director", StringComparison.OrdinalIgnoreCase) ||
                Session.UsuarioActual.Rol.Equals("Sistemas", StringComparison.OrdinalIgnoreCase) )
            {
                EsModoTecnico = true;
            }
            else
            {
                EsModoTecnico = false;
            }
        }
        private void GuardarPreferenciaSucursal()
        {
            Properties.Settings.Default.SucursalDefaultId = MiSucursalDefault;
            Properties.Settings.Default.Save();
            _notificationService.ShowSuccess("Sucursal predeterminada actualizada.");
        }

        // =========================================================================
        // LÓGICA DE CONEXIÓN SQL
        // =========================================================================
        private void CargarDatosConexion()
        {
            ServerIP = Properties.Settings.Default.Data_Server;
            DatabaseName = Properties.Settings.Default.Data_Db;
            DbUser = Properties.Settings.Default.Data_User;
            // La contraseña no la cargamos al PasswordBox por seguridad (y porque WPF no deja bindearla fácil)
            // El usuario tendrá que escribirla si quiere cambiarla o probarla.
        }

        private async void ProbarConexion(object parameter)
        {
            // Verificamos que el parámetro sea el PasswordBox
            if ( parameter is System.Windows.Controls.PasswordBox passBox )
            {
                string password = passBox.Password;
                string tipoString = passBox.Tag.ToString(); // Aquí recibes "Auth" o "Data"
                string connectionStringOverride = "";

                // Variable para el Enum que pide el SqlHelper
                TipoConexion tipoEnum;

                // Lógica para armar la cadena según qué botón presionaron
                if ( tipoString == "Auth" )
                {
                    tipoEnum = TipoConexion.Auth; // <--- Asignamos el Enum correcto
                    connectionStringOverride = $"Data Source={AuthServer};Initial Catalog={AuthDb};User ID={AuthUser};Password={password};TrustServerCertificate=True;Timeout=5";
                }
                else // Es "Data"
                {
                    tipoEnum = TipoConexion.Data; // <--- Asignamos el Enum correcto
                    connectionStringOverride = $"Data Source={DataServer};Initial Catalog={DataDb};User ID={DataUser};Password={password};TrustServerCertificate=True;Timeout=5";
                }

                try
                {
                    // AHORA SÍ: Pasamos el Enum (tipoEnum) y el string (connectionStringOverride)
                    var helper = new SqlHelper(tipoEnum, connectionStringOverride);

                    bool exito = await helper.ProbarConexionAsync();

                    if ( exito )
                        _notificationService.ShowSuccess($"Conexión a {tipoString} exitosa.");
                    else
                        _dialogService.ShowMessage("Error", $"No se pudo conectar al servidor {tipoString}. Revisa los datos.");
                }
                catch ( Exception ex )
                {
                    _dialogService.ShowMessage("Error", "Ocurrió un error al intentar conectar: " + ex.Message);
                }
            }
        }

        private void GuardarConexion(object parameter)
        {
            if ( parameter is PasswordBox passBox )
            {
                var secure = new SecureStorageService();
                string tipo = passBox.Tag.ToString(); // "Auth" o "Data"

                if ( tipo == "Auth" )
                {
                    Properties.Settings.Default.Auth_Server = AuthServer;
                    Properties.Settings.Default.Auth_Db = AuthDb;
                    Properties.Settings.Default.Auth_User = AuthUser;
                    secure.GuardarPassword(passBox.Password, SecureStorageService.KeyAuth);
                }
                else if ( tipo == "Data" )
                {
                    Properties.Settings.Default.Data_Server = DataServer;
                    Properties.Settings.Default.Data_Db = DataDb;
                    Properties.Settings.Default.Data_User = DataUser;
                    secure.GuardarPassword(passBox.Password, SecureStorageService.KeyData);
                }

                Properties.Settings.Default.Save();
                _notificationService.ShowSuccess($"Conexión {tipo} guardada.");

                // Opcional: Reiniciar la aplicación para tomar los cambios
                bool reiniciar = _dialogService.ShowConfirmation("Reinicio Requerido", "Para aplicar la nueva conexión, es necesario reiniciar. ¿Deseas hacerlo ahora?");
                if ( reiniciar )
                {
                    // 1. Obtener la ruta del ejecutable actual (.exe)
                    var nombreEjecutable = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                    // 2. Iniciar una nueva instancia de ese ejecutable
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(nombreEjecutable) { UseShellExecute = true });

                    // 3. Matar la instancia actual
                    System.Windows.Application.Current.Shutdown();
                }
            }
        }
    }
}