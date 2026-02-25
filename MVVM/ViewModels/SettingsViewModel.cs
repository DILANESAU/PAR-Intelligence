using MaterialDesignThemes.Wpf;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using WPF_PAR.Converters;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;
using WPF_PAR.Core.Services; // Para SqlHelper y SucursalesService
using WPF_PAR.Core.Models;   // Para Session si es necesario

// Importamos el enum TipoConexion si está dentro de SqlHelper
using static WPF_PAR.Core.Services.SqlHelper;

namespace WPF_PAR.MVVM.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        // --- SERVICIOS ---
        private readonly SucursalesService _sucursalesService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly ThemeService _themeService;

        // --- PROPIEDADES ---
        public string AuthServer { get; set; }
        public string AuthDb { get; set; }
        public string AuthUser { get; set; }
        public string DataServer { get; set; }
        public string DataDb { get; set; }
        public string DataUser { get; set; }

        private bool _esModoTecnico;
        public bool EsModoTecnico
        {
            get => _esModoTecnico;
            set { _esModoTecnico = value; OnPropertyChanged(); }
        }

        private Dictionary<int, string> _misSucursales;
        public Dictionary<int, string> MisSucursales
        {
            get => _misSucursales;
            set { _misSucursales = value; OnPropertyChanged(); }
        }

        private int _miSucursalDefault;
        public int MiSucursalDefault
        {
            get => _miSucursalDefault;
            set { _miSucursalDefault = value; OnPropertyChanged(); }
        }

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
                    Properties.Settings.Default.IsDarkMode = value;
                    Properties.Settings.Default.Save();
                }
            }
        }

        // --- COMANDOS ---
        public RelayCommand GuardarSucursalCommand { get; set; }
        public RelayCommand ProbarConexionCommand { get; set; }
        public RelayCommand GuardarConexionCommand { get; set; }
        public RelayCommand VerLogsCommand { get; set; }
        public RelayCommand ContactarSoporteCommand { get; set; }

        // =========================================================================
        // CONSTRUCTOR ACTUALIZADO (Sin parámetros para MainViewModel)
        // =========================================================================
        public SettingsViewModel()
        {
            // 1. Instanciar servicios de UI
            _dialogService = new DialogService();
            _notificationService = new NotificationService();
            _themeService = new ThemeService();

            // 2. Obtener conexión actual para inicializar SucursalesService
            // Construimos una cadena temporal con lo que hay en Settings para poder cargar la lista
            string server = Properties.Settings.Default.Data_Server;
            string db = Properties.Settings.Default.Data_Db;
            string user = Properties.Settings.Default.Data_User;

            // Recuperamos el password del storage seguro
            var secure = new SecureStorageService();
            string pass = secure.RecuperarPassword(SecureStorageService.KeyData);

            string connString = $"Data Source={server};Initial Catalog={db};User ID={user};Password={pass};TrustServerCertificate=True;";

            _sucursalesService = new SucursalesService(connString);

            // 3. Cargar datos de la vista
            DeterminarPermisos();
            _isDarkMode = Properties.Settings.Default.IsDarkMode;
            CargarDatosConexion();
            CargarConfiguracionSucursales();

            // 4. Inicializar Comandos
            GuardarSucursalCommand = new RelayCommand(o => GuardarPreferenciaSucursal());

            ProbarConexionCommand = new RelayCommand(param => ProbarConexion(param));
            GuardarConexionCommand = new RelayCommand(param => GuardarConexion(param));

            VerLogsCommand = new RelayCommand(o => System.Diagnostics.Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory));
            ContactarSoporteCommand = new RelayCommand(o => _dialogService.ShowMessage("Soporte", "Envía un correo a sistemas@par.com"));
        }

        private void CargarConfiguracionSucursales()
        {
            try
            {
                var todas = _sucursalesService.CargarSucursales();
                bool esSuperUsuario = Session.UsuarioActual?.Rol == "Director" ||
                                      Session.UsuarioActual?.Rol == "Sistemas" ||
                                      Session.UsuarioActual?.Rol == "Admin";

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

                if ( MisSucursales.Count == 0 ) MisSucursales.Add(0, "0 - TODAS");

                int guardada = Properties.Settings.Default.SucursalDefaultId;
                MiSucursalDefault = MisSucursales.ContainsKey(guardada) ? guardada : MisSucursales.Keys.First();
            }
            catch
            {
                MisSucursales = new Dictionary<int, string> { { 0, "0 - TODAS" } };
                MiSucursalDefault = 0;
            }
        }

        private void DeterminarPermisos()
        {
            if ( Session.UsuarioActual == null ) { EsModoTecnico = true; return; }

            EsModoTecnico = Session.UsuarioActual.Rol.Equals("Director", StringComparison.OrdinalIgnoreCase) ||
                            Session.UsuarioActual.Rol.Equals("Sistemas", StringComparison.OrdinalIgnoreCase);
        }

        private void GuardarPreferenciaSucursal()
        {
            Properties.Settings.Default.SucursalDefaultId = MiSucursalDefault;
            Properties.Settings.Default.Save();
            _notificationService.ShowSuccess("Sucursal predeterminada actualizada.");
        }

        private void CargarDatosConexion()
        {
            AuthServer = Properties.Settings.Default.Auth_Server;
            AuthDb = Properties.Settings.Default.Auth_Db;
            AuthUser = Properties.Settings.Default.Auth_User;

            DataServer = Properties.Settings.Default.Data_Server;
            DataDb = Properties.Settings.Default.Data_Db;
            DataUser = Properties.Settings.Default.Data_User;
        }

        private async void ProbarConexion(object parameter)
        {
            if ( parameter is PasswordBox passBox )
            {
                string password = passBox.Password;
                // El Tag del PasswordBox nos dice qué botón se presionó ("Auth" o "Data")
                string tipoString = passBox.Tag?.ToString() ?? "Data";

                string connectionStringGenerada = "";

                if ( tipoString == "Auth" )
                {
                    connectionStringGenerada = $"Data Source={AuthServer};Initial Catalog={AuthDb};User ID={AuthUser};Password={password};TrustServerCertificate=True;Timeout=5";
                }
                else
                {
                    connectionStringGenerada = $"Data Source={DataServer};Initial Catalog={DataDb};User ID={DataUser};Password={password};TrustServerCertificate=True;Timeout=5";
                }

                try
                {

                    var helper = new SqlHelper(connectionStringGenerada);

                    // 3. PROBAMOS
                    bool exito = await helper.ProbarConexionAsync();

                    if ( exito )
                        _notificationService.ShowSuccess($"Conexión a {tipoString} exitosa.");
                    else
                        _dialogService.ShowMessage("Error", $"No se pudo conectar al servidor {tipoString}. Verifica los datos.");
                }
                catch ( Exception ex )
                {
                    _dialogService.ShowMessage("Error Crítico", "Ocurrió una excepción al conectar: " + ex.Message);
                }
            }
        }

        private void GuardarConexion(object parameter)
        {
            if ( parameter is PasswordBox passBox )
            {
                var secure = new SecureStorageService();
                string tipo = passBox.Tag?.ToString() ?? "Data";

                if ( tipo == "Auth" )
                {
                    Properties.Settings.Default.Auth_Server = AuthServer;
                    Properties.Settings.Default.Auth_Db = AuthDb;
                    Properties.Settings.Default.Auth_User = AuthUser;
                    secure.GuardarPassword(passBox.Password, SecureStorageService.KeyAuth);
                }
                else
                {
                    Properties.Settings.Default.Data_Server = DataServer;
                    Properties.Settings.Default.Data_Db = DataDb;
                    Properties.Settings.Default.Data_User = DataUser;
                    secure.GuardarPassword(passBox.Password, SecureStorageService.KeyData);
                }

                Properties.Settings.Default.Save();
                _notificationService.ShowSuccess($"Conexión {tipo} guardada.");

                if ( _dialogService.ShowConfirmation("Reinicio Requerido", "¿Deseas reiniciar ahora?") )
                {
                    var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
                    System.Windows.Application.Current.Shutdown();
                }
            }
        }
    }
}