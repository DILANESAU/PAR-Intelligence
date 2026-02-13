using Microsoft.Extensions.DependencyInjection;

using System.Windows;
using System.Linq;

using WPF_PAR.MVVM.ViewModels;
using WPF_PAR.MVVM.Views;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;
using WPF_PAR.Core.Services; // IMPORTANT: Using for Core services
using WPF_PAR.Core.Models;

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
            services.AddSingleton<FilterService>();
            services.AddSingleton<BusinessLogicService>();

            // KEY: Register SecureStorage so we can use it in the factories below
            services.AddSingleton<SecureStorageService>();

            // =========================================================================
            // 2. CORE SERVICES (Injecting Connection Strings) 💉
            // =========================================================================

            // A) REPORTES SERVICE (Connects to Intelisis DB)
            services.AddTransient<ReportesService>(provider =>
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;

                // Recover password. If null (first run), use empty string to avoid crash.
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";

                // Build the connection string dynamically
                string connectionString = $"Data Source={settings.Data_Server};Initial Catalog={settings.Data_Db};User ID={settings.Data_User};Password={pass};TrustServerCertificate=True;Timeout=60";

                // Inject the string into the Core service
                return new ReportesService(connectionString);
            });

            // B) SUCURSALES SERVICE (Connects to Intelisis DB)
            services.AddTransient<SucursalesService>(provider =>
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";

                string connectionString = $"Data Source={settings.Data_Server};Initial Catalog={settings.Data_Db};User ID={settings.Data_User};Password={pass};TrustServerCertificate=True";

                return new SucursalesService(connectionString);
            });

            // C) AUTH SERVICE (Connects to PAR_System_DB)
            services.AddTransient<AuthService>(provider =>
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";

                // NOTE: Assuming PAR_System_DB is on the same server as DataServer
                // If it were separate, you would use settings.AuthServer
                string connectionString = $"Data Source={settings.Data_Server};Initial Catalog=PAR_System_DB;User ID={settings.Data_User};Password={pass};TrustServerCertificate=True";

                return new AuthService(connectionString);
            });

            // =========================================================================
            // 3. LOGIC SERVICES (Pure Logic from Core)
            // =========================================================================
            // These don't need a factory because they don't use SQL directly or their dependencies are auto-resolved
            services.AddTransient<FamiliaLogicService>();
            services.AddTransient<ClientesLogicService>();
            services.AddTransient<ChartService>();
            services.AddTransient<ClientesService>();
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
            services.AddTransient<SettingsView>(); // If you use it as a window or direct view

            return services.BuildServiceProvider();
        }
        protected override void OnStartup(StartupEventArgs e)
        {   
            if ( WPF_PAR.Properties.Settings.Default.UpgradeRequired )
            {
                try
                {
                    // 1. Busca la configuración de la versión anterior y la importa
                    WPF_PAR.Properties.Settings.Default.Upgrade();

                    // 2. Apagamos la bandera para que no lo haga cada vez que abres la app
                    WPF_PAR.Properties.Settings.Default.UpgradeRequired = false;

                    // 3. Guardamos los cambios en la NUEVA carpeta de versión
                    WPF_PAR.Properties.Settings.Default.Save();
                }
                catch ( Exception ex )
                {
                    // Si falla (muy raro), al menos no crasheamos, pero tocará reconfigurar
                    System.Diagnostics.Debug.WriteLine("Error migrando settings: " + ex.Message);
                }
            }
            base.OnStartup(e);

            // 1. Validar Config Auth
            string authIp = WPF_PAR.Properties.Settings.Default.Auth_Server;
            var secure = new SecureStorageService();
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

        // Modificamos el método para aceptar un parámetro opcional
        public void AbrirMainWindow(bool modoConfiguracion = false)
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            var mainViewModel = Services.GetRequiredService<MainViewModel>();

            if ( modoConfiguracion )
            {
                // Forzamos la vista de Settings
                mainViewModel.CurrentView = Services.GetRequiredService<SettingsViewModel>();

                // Mensaje de bienvenida
                mainViewModel.MessageQueue.Enqueue("Bienvenido. Configura la conexión al servidor para continuar.");
            }
            else
            {
                // Flujo normal (viene del Login)
                // 1. Obtenemos el DashboardViewModel
                var dashboardVM = Services.GetRequiredService<DashboardViewModel>();

                // 2. Lo asignamos como vista actual
                mainViewModel.CurrentView = dashboardVM;

                dashboardVM.CargarDatosIniciales();
            }

            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();

            // Asegurarnos de cerrar la ventana de Login si estaba abierta
            var loginWindow = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
            if ( loginWindow != null )
            {
                loginWindow.Close();
            }
        }
    }
}