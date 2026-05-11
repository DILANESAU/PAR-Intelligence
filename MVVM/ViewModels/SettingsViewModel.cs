using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using WPF_PAR.Converters;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;
using WPF_PAR.Core.Services;
using WPF_PAR.Core.Models;
using WPF_PAR.MVVM.Views;

namespace WPF_PAR.MVVM.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        // --- SERVICIOS ---
        private readonly SucursalesService _sucursalesService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly ThemeService _themeService;

        public event Action<object> PeticionNavegacion;

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
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                    _themeService.SetThemeMode(_isDarkMode);
                    Properties.Settings.Default.IsDarkMode = value;
                    Properties.Settings.Default.Save();
                }
            }
        }

        public GestionUsuariosViewModel GestionUsuariosVM { get; set; }
        public RelayCommand VerLogsCommand { get; set; }
        public RelayCommand ContactarSoporteCommand { get; set; }
        public RelayCommand AbrirAltaUsuarioCommand { get; set; }
        public RelayCommand VerUsuariosCommand { get; set; }

        public SettingsViewModel()
        {
            _dialogService = new DialogService();
            _notificationService = new NotificationService();
            _themeService = new ThemeService();

            string cadenaConexion = System.Configuration.ConfigurationManager.ConnectionStrings["ParSystem"]?.ConnectionString ?? "";

            DeterminarPermisos();
            _isDarkMode = Properties.Settings.Default.IsDarkMode;

            _sucursalesService = new SucursalesService(cadenaConexion);
            VerLogsCommand = new RelayCommand(o => System.Diagnostics.Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory));
            ContactarSoporteCommand = new RelayCommand(o => _dialogService.ShowMessage("Soporte", "Envía un correo a sistemas@par.com"));
            CargarConfiguracionSucursales();
            AbrirAltaUsuarioCommand = new RelayCommand(async o =>
            {
                try
                {
                    var vistaModal = new AltaUsuarioView
                    {
                        DataContext = new AltaUsuarioViewModel()
                    };

                    //await DialogHost.Show(vistaModal, "RootDialog");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Error al abrir el pop-up: " + ex.Message);
                }
            });
            GestionUsuariosVM = new GestionUsuariosViewModel();
            VerUsuariosCommand = new RelayCommand(o =>
            {
                PeticionNavegacion?.Invoke(GestionUsuariosVM);
            });

        }
        private void CargarConfiguracionSucursales()
        {
            try
            {
                var todas = _sucursalesService.CargarSucursales() ?? new Dictionary<int, string>();
                bool esSuperUsuario = Session.UsuarioActual?.Rol == "Director" ||
                                      Session.UsuarioActual?.Rol == "Sistemas" ||
                                      Session.UsuarioActual?.Rol == "Administrador";

                if (esSuperUsuario || Session.UsuarioActual?.SucursalesPermitidas == null)
                {
                    MisSucursales = todas;
                }
                else
                {
                    MisSucursales = todas
                        .Where(s => Converters.Session.UsuarioActual.SucursalesPermitidas.Contains(s.Key))
                        .ToDictionary(k => k.Key, v => v.Value);
                }

                if (MisSucursales == null || !MisSucursales.Any())
                {
                    MisSucursales = new Dictionary<int, string> { { -1, "SIN SUCURSALES ASIGNADAS" } };
                }

                int guardada = Properties.Settings.Default.SucursalDefaultId;
                MiSucursalDefault = MisSucursales.ContainsKey(guardada) ? guardada : MisSucursales.Keys.FirstOrDefault();
            }
            catch
            {
                MisSucursales = new Dictionary<int, string> { { 0, "0 - TODAS" } };
                MiSucursalDefault = 0;
            }
        }
        private void DeterminarPermisos()
        {
            if (Session.UsuarioActual == null) { EsModoTecnico = true; return; }

            EsModoTecnico = Session.UsuarioActual.Rol.Equals("Director", StringComparison.OrdinalIgnoreCase) ||
                            Session.UsuarioActual.Rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase) || Session.UsuarioActual.Rol.Equals("Sistemas", StringComparison.OrdinalIgnoreCase) ;
        }
    }
}