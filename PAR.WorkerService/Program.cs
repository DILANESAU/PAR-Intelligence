using PAR.WorkerService;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // <--- NUEVO: Para configurar los logs
using Microsoft.Extensions.Logging.EventLog; // <--- NUEVO: Para el Visor de Eventos

// Configuración del Host para el Worker Service
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        // Este es el nombre con el que aparecerá en la lista de Servicios de Windows
        options.ServiceName = "PAR Intelligence Worker";
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders(); // Limpiamos la basura por defecto
        logging.AddConsole();     // Mantiene los logs en la consolita negra (cuando depuras en VS)
        EventLogSettings settings = new()
        {
            SourceName = "PAR Intelligence Worker", // <--- Con este nombre exacto lo vas a buscar
            LogName = "Application"                 // <--- Se guardará en la carpeta "Aplicación" de Windows
        };

        // ¡LA MAGIA PARA EL VISOR DE EVENTOS!
        logging.AddEventLog(settings);
    })
    .ConfigureServices(services =>
    {
        // Solo registramos el Worker. 
        // Él se encargará de leer el appsettings.json y crear sus servicios internamente.
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();