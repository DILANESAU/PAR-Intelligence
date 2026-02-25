using MaterialDesignThemes.Wpf;

using System;
using System.Collections.Generic;

using WPF_PAR.Converters;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        public FilterService GlobalFilters { get; }
        public Dictionary<int, string> ListaSucursales { get; set; }

        // --- PROPIEDADES DE USUARIO ---
        private string _userName;
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        private string _userRol;
        public string UserRol
        {
            get => _userRol;
            set { _userRol = value; OnPropertyChanged(); }
        }

        // --- COMANDOS ---
        public RelayCommand DashboardViewCommand { get; set; }
        public RelayCommand FamiliaViewCommand { get; set; }
        public RelayCommand ClientesViewCommand { get; set; }
        public RelayCommand SettingsViewCommand { get; set; }
        public RelayCommand NavegarLineaCommand { get; set; }
        public RelayCommand ToggleMenuCommand { get; set; }

        // --- VIEWMODELS HIJOS ---
        public DashboardViewModel DashboardVM { get; }
        public FamiliaViewModel FamiliaVM { get; }
        public ClientesViewModel ClientesVM { get; }
        public SettingsViewModel SettingsVM { get; }

        // --- ESTADO DE LA VISTA ---
        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged();
                // Lógica automática: Ocultar filtros si es Settings
                AreFiltersVisible = !( value is SettingsViewModel );
            }
        }

        private bool _isMenuOpen = true;
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set { _isMenuOpen = value; OnPropertyChanged(); }
        }

        private bool _areFiltersVisible = true;
        public bool AreFiltersVisible
        {
            get => _areFiltersVisible;
            set { _areFiltersVisible = value; OnPropertyChanged(); }
        }

        public SnackbarMessageQueue MessageQueue { get; }

        // ==============================================================================
        // CONSTRUCTOR CORREGIDO (Inyección de Dependencias)
        // Pedimos los ViewModels y Servicios en lugar del connectionString
        // ==============================================================================
        public MainViewModel(
            FilterService filterService,
            INotificationService notificationService,
            DashboardViewModel dashboardVM,
            FamiliaViewModel familiaVM,
            ClientesViewModel clientesVM,
            SettingsViewModel settingsVM)
        {
            // 1. Asignamos los servicios inyectados
            GlobalFilters = filterService;
            ListaSucursales = GlobalFilters.ListaSucursales;

            // Extraemos la cola de mensajes (asegurando el cast)
            if ( notificationService is NotificationService ns )
            {
                MessageQueue = ns.MessageQueue;
            }

            // 2. Asignamos los ViewModels que nos entregó App.xaml.cs
            DashboardVM = dashboardVM;
            FamiliaVM = familiaVM;
            ClientesVM = clientesVM;
            SettingsVM = settingsVM;

            // 3. Cargar datos de sesión
            if ( Session.UsuarioActual != null )
            {
                UserName = Session.UsuarioActual.NombreCompleto;
                UserRol = Session.UsuarioActual.Rol;
            }
            else
            {
                UserName = "Usuario";
                UserRol = "Invitado";
            }

            // 4. Configurar Comandos de Navegación
            DashboardViewCommand = new RelayCommand(o =>
            {
                CurrentView = DashboardVM;
                DashboardVM.CargarDatosIniciales();
            });

            FamiliaViewCommand = new RelayCommand(o =>
            {
                CurrentView = FamiliaVM;
                FamiliaVM.CargarDatosIniciales();
            });

            ClientesViewCommand = new RelayCommand(o =>
            {
                FamiliaVM.DetenerRenderizado(); // Optimización
                CurrentView = ClientesVM;
                ClientesVM.CargarDatosIniciales();
            });

            SettingsViewCommand = new RelayCommand(o => CurrentView = SettingsVM);

            NavegarLineaCommand = new RelayCommand(parametro =>
            {
                if ( parametro is string linea )
                {
                    CurrentView = FamiliaVM;
                    FamiliaVM.CargarPorLinea(linea);
                }
            });

            ToggleMenuCommand = new RelayCommand(o => IsMenuOpen = !IsMenuOpen);

            // Nota: La vista inicial ("CurrentView = DashboardVM") ahora 
            // se maneja desde el App.xaml.cs en la función AbrirMainWindow()
        }
    }
}