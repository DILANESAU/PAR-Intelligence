using Microsoft.Extensions.DependencyInjection;

using System;
using System.Windows;
using System.Linq;
using System.Configuration;
using WPF_PAR.MVVM.ViewModels;
using WPF_PAR.MVVM.Views;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;
using WPF_PAR.Core.Services;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services.Interfaces;

namespace WPF_PAR
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // =========================================================================
            // 1. INFRASTRUCTURE SERVICES (UI and Local Tools)
            // =========================================================================
            services.AddSingleton<ThemeService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<SecureStorageService>();
            services.AddSingleton<IBusinessLogicService, BusinessLogicService>();
            services.AddSingleton<IClientesLogicService, ClientesLogicService>();

            // =========================================================================
            // 2. CORE SERVICES (Injecting Connection Strings) 💉
            // =========================================================================
            string GetParSystemConnection()
            {
                var connStr = System.Configuration.ConfigurationManager.ConnectionStrings["ParSystem"];
                if (connStr == null || string.IsNullOrWhiteSpace(connStr.ConnectionString))
                {
                    MessageBox.Show("Falta la cadena de conexión 'ParSystem' en el archivo App.config.", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1); // Cierra la app si TI no la configuró
                }
                return connStr.ConnectionString;
            }

            string connectionString = GetParSystemConnection();

            services.AddTransient<ReportesService>(provider => new ReportesService(connectionString));
            services.AddTransient<SucursalesService>(provider => new SucursalesService(connectionString));
            services.AddTransient<ClientesService>(provider => new ClientesService(connectionString));
            services.AddSingleton<FilterService>(provider => new FilterService(connectionString));
            services.AddTransient<AuthService>(provider => new AuthService(connectionString));
            services.AddTransient<CacheService>(provider => new CacheService(connectionString));

            // =========================================================================
            // 3. LOGIC SERVICES (Pure Logic from Core - No SQL needed)
            // =========================================================================
            services.AddTransient<FamiliaLogicService>();
            services.AddTransient<ChartService>();
            services.AddTransient<CatalogoService>();
            services.AddTransient<ExportService>();

            // =========================================================================
            // 4. VIEW MODELS & VIEWS
            // =========================================================================
            services.AddSingleton<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<FamiliaViewModel>();
            services.AddTransient<ClientesViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<LoginViewModel>();

            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<SettingsView>();

            return services.BuildServiceProvider();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            if ( WPF_PAR.Properties.Settings.Default.UpgradeRequired )
            {
                try
                {
                    WPF_PAR.Properties.Settings.Default.Upgrade();
                    WPF_PAR.Properties.Settings.Default.UpgradeRequired = false;
                    WPF_PAR.Properties.Settings.Default.Save();
                }
                catch ( Exception ex )
                {
                    System.Diagnostics.Debug.WriteLine("Error migrando settings: " + ex.Message);
                }
            }
            base.OnStartup(e);


            var loginWindow = Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();
        }

        public void AbrirMainWindow()
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            var mainViewModel = Services.GetRequiredService<MainViewModel>();

            var dashboardVM = Services.GetRequiredService<DashboardViewModel>();
            mainViewModel.CurrentView = dashboardVM;
            dashboardVM.CargarDatosIniciales();

            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();

            var loginWindow = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
            if (loginWindow != null)
            {
                loginWindow.Close();
            }
        }
    }
}