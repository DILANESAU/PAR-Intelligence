using PAR.WorkerService;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configuración del Host para el Worker Service
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Solo registramos el Worker. 
        // Él se encargará de leer el appsettings.json y crear sus servicios internamente.
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();