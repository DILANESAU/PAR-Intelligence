using PAR.WorkerService;

using WPF_PAR.Core.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        IConfiguration configuration = hostContext.Configuration;
        string connIntelisis = configuration.GetConnectionString("Intelisis");
        string connPAR = configuration.GetConnectionString("ParSystemConnection");
        services.AddSingleton<ReportesService>(sp => new ReportesService(connIntelisis));
        services.AddSingleton<ClientesService>(sp => new ClientesService(connIntelisis));
        services.AddSingleton<CacheService>(sp => new CacheService(connPar));

        services.AddHostedService<Worker>();
    })
    .Build();
/*
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
*/