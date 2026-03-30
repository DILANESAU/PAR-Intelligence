using Microsoft.Extensions.DependencyInjection;

using System;
using System.Windows;
using System.Linq;

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

            // Métodos auxiliares para no repetir el código de sacar la contraseña
            string GetIntelisisConnection(IServiceProvider provider)
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";
                return $"Data Source={settings.Data_Server};Initial Catalog={settings.Data_Db};User ID={settings.Data_User};Password={pass};TrustServerCertificate=True;Timeout=60";
            }

            string GetParSystemConnection(IServiceProvider provider)
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;

                // USAR LAS LLAVES DE AUTH (MINI PC)
                string pass = secure.RecuperarPassword(SecureStorageService.KeyAuth) ?? "";

                // USAR LOS SETTINGS DE AUTH (MINI PC)
                return $"Data Source={settings.Auth_Server};Initial Catalog=PAR_System_DB;User ID={settings.Auth_User};Password={pass};TrustServerCertificate=True";
            }

            // A) Servicios que leen del ERP (Intelisis)
            services.AddTransient<ReportesService>(provider => new ReportesService(GetParSystemConnection(provider)));
            services.AddTransient<SucursalesService>(provider => new SucursalesService(GetParSystemConnection(provider)));
            services.AddTransient<ClientesService>(provider => new ClientesService(GetParSystemConnection(provider)));
            services.AddSingleton<FilterService>(provider => new FilterService(GetParSystemConnection(provider))); ;

            // B) Servicios que leen/escriben de tu BD Local/Intermedia (ParSystem)
            services.AddTransient<AuthService>(provider => new AuthService(GetParSystemConnection(provider)));
            services.AddTransient<CacheService>(provider => new CacheService(GetParSystemConnection(provider)));

            // =========================================================================
            // 3. LOGIC SERVICES (Pure Logic from Core - No SQL needed)
            // =========================================================================
            services.AddTransient<FamiliaLogicService>();
            //services.AddTransient<ClientesLogicService>();
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

        protected override void OnStartup(StartupEventArgs e)
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

            // 1. Validar Config Auth
            string authIp = WPF_PAR.Properties.Settings.Default.Auth_Server;
            var secure = Services.GetRequiredService<SecureStorageService>();
            string authPass = secure.RecuperarPassword(SecureStorageService.KeyAuth);

            // 2. Validar Config Data
            string dataIp = WPF_PAR.Properties.Settings.Default.Data_Server;
            string dataPass = secure.RecuperarPassword(SecureStorageService.KeyData);

            // Si falta CUALQUIERA de los dos, forzamos configuración
            bool faltaConfig = string.IsNullOrEmpty(authIp) || string.IsNullOrEmpty(authPass) ||
                               string.IsNullOrEmpty(dataIp) || string.IsNullOrEmpty(dataPass);

            if ( faltaConfig )
            {
                AbrirMainWindow(modoConfiguracion: true);
            }
            else
            {
                var loginWindow = Services.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
        }

        public void AbrirMainWindow(bool modoConfiguracion = false)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            var mainViewModel = Services.GetRequiredService<MainViewModel>();

            if ( modoConfiguracion )
            {
                mainViewModel.CurrentView = Services.GetRequiredService<SettingsViewModel>();
                mainViewModel.MessageQueue.Enqueue("Bienvenido. Configura la conexión al servidor para continuar.");
            }
            else
            {
                var dashboardVM = Services.GetRequiredService<DashboardViewModel>();
                mainViewModel.CurrentView = dashboardVM;
                dashboardVM.CargarDatosIniciales();
            }

            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();

            var loginWindow = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
            if ( loginWindow != null )
            {
                loginWindow.Close();
            }
        }
    }
}