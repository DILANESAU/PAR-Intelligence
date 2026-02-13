using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Linq; // Necesario para cerrar ventanas
using WPF_PAR.MVVM.ViewModels;
using WPF_PAR.MVVM.Views;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

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
            // 1. SERVICIOS DE INFRAESTRUCTURA (UI y Herramientas Locales)
            // =========================================================================
            services.AddSingleton<ThemeService>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<FilterService>();
            services.AddSingleton<BusinessLogicService>(); // Reglas de negocio visuales

            // IMPORTANTE: Registramos SecureStorageService para poder usarlo abajo 👇
            services.AddSingleton<SecureStorageService>();

            // =========================================================================
            // 2. SERVICIOS DEL CORE (Aquí inyectamos la cadena de conexión) 💉
            // =========================================================================

            // A) REPORTES SERVICE (Lee datos de Intelisis)
            services.AddTransient<ReportesService>(provider =>
            {
                // 1. Pedimos las herramientas necesarias
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;

                // 2. Recuperamos la contraseña segura
                // Si es null (primera vez), ponemos cadena vacía para evitar crash
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";

                // 3. Armamos la cadena de conexión completa
                string connectionString = $"Data Source={settings.DataServer};Initial Catalog={settings.DataDb};User ID={settings.DataUser};Password={pass};TrustServerCertificate=True;Timeout=60";

                // 4. Creamos y devolvemos el servicio listo
                return new ReportesService(connectionString);
            });

            // B) AUTH SERVICE (Gestiona usuarios en PAR_System_DB)
            services.AddTransient<AuthService>(provider =>
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";

                // NOTA: Asumimos que PAR_System_DB está en el mismo servidor que Intelisis
                // Si estuviera en otro, usarías settings.AuthServer, etc.
                string connectionString = $"Data Source={settings.DataServer};Initial Catalog=PAR_System_DB;User ID={settings.DataUser};Password={pass};TrustServerCertificate=True";

                return new AuthService(connectionString);
            });

            // C) SUCURSALES SERVICE (Lee sucursales de Intelisis)
            services.AddTransient<SucursalesService>(provider =>
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";

                string connectionString = $"Data Source={settings.DataServer};Initial Catalog={settings.DataDb};User ID={settings.DataUser};Password={pass};TrustServerCertificate=True";

                return new SucursalesService(connectionString);
            });

            // D) CACHE DB SERVICE (Nuevo servicio para leer lo que procesa el Worker)
            // Este servicio leerá de PAR_System_DB para pintar gráficas rápido
            services.AddTransient<CacheDbService>(provider =>
            {
                var secure = provider.GetRequiredService<SecureStorageService>();
                var settings = WPF_PAR.Properties.Settings.Default;
                string pass = secure.RecuperarPassword(SecureStorageService.KeyData) ?? "";

                string connectionString = $"Data Source={settings.DataServer};Initial Catalog=PAR_System_DB;User ID={settings.DataUser};Password={pass};TrustServerCertificate=True";

                return new CacheDbService(connectionString);
            });

            // =========================================================================
            // 3. LÓGICA DE NEGOCIO (Core Pura)
            // =========================================================================
            // Estos servicios no necesitan "Fábrica" especial porque no usan SQL directo,
            // o porque sus dependencias ya se inyectan automáticamente.
            services.AddTransient<FamiliaLogicService>();
            services.AddTransient<ClientesLogicService>();
            services.AddTransient<ChartService>();
            services.AddTransient<ClientesService>(); // Este quizás dependa de ReportesService, y se inyectará solo.
            services.AddTransient<CatalogoService>();

            // =========================================================================
            // 4. VISTAS Y MODELOS DE VISTA (MVVM)
            // =========================================================================
            services.AddSingleton<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<FamiliaViewModel>();
            services.AddTransient<ClientesViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<LoginViewModel>();

            services.AddTransient<MainWindow>();
            services.AddTransient<LoginWindow>();

            return services.BuildServiceProvider();
        }

        // =========================================================
        // AQUÍ ESTÁ EL CAMBIO CLAVE
        // =========================================================
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

                // 3. ¡ESTA ES LA LÍNEA QUE FALTA! 🚀
                // Disparamos la carga inicial para que busque las sucursales y datos
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