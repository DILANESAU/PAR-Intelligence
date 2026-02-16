using Dapper; // ¡Importante!

using Microsoft.Data.SqlClient;

using System.Text.Json;

using WPF_PAR.Core.Services;

using WPF_PAR.Core.Models;

namespace PAR.WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        private readonly ReportesService _reportesService;
        private readonly ClientesService _clientesService;
        private readonly CacheService _cacheService;

        // El constructor recibe todo mágicamente gracias a Program.cs
        public Worker(ILogger<Worker> logger,
                      IConfiguration configuration,
                      ReportesService reportesService,
                      ClientesService clientesService,
                      CacheService cacheService)
        {
            _logger = logger;
            _configuration = configuration;
            _reportesService = reportesService;
            _clientesService = clientesService;
            _cacheService = cacheService;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while ( !stoppingToken.IsCancellationRequested )
            {
                try
                {
                    // 1. Obtener cadenas
                    string connIntelisis = _config.GetConnectionString("Intelisis");
                    string connPar = _config.GetConnectionString("ParSystem");
                    var sucursales = _config.GetSection("Config:SucursalesIds").Get<int[]>();

                    // 2. Instanciar servicios
                    var reportesService = new ReportesService(connIntelisis);
                    var familiaLogic = new FamiliaLogicService(); // ¡Tu lógica existente!
                    var cacheService = new CacheService(connPar); // ¡El nuevo servicio!

                    foreach ( var sucId in sucursales )
                    {
                        _logger.LogInformation($"Procesando Sucursal {sucId}...");

                        // A. LEER (Pesado)
                        var ventas = await reportesService.ObtenerVentasBrutasRango(sucId, InicioMes(), DateTime.Now);

                        // B. CALCULAR (Lógica de Negocio)
                        var (arqui, espe) = familiaLogic.CalcularResumenGlobal(ventas);
                        var todas = arqui.Concat(espe).ToList();

                        // C. GUARDAR (Rápido)
                        await cacheService.GuardarFamiliasAsync(sucId, todas);
                    }

                    _logger.LogInformation("✅ Ciclo completado.");
                }
                catch ( Exception ex )
                {
                    _logger.LogError(ex, "Error en worker");
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        private async Task ProcesarSucursal(int idSucursal)
        {
            _logger.LogInformation($"   > Procesando Sucursal {idSucursal}...");


            // 1. Obtener datos complejos de Clientes (con lógica YTD, semestres, etc.)
            var analisisClientes = await _clientesService.ObtenerDatosBase(DateTime.Now.Year, idSucursal);

            // 2. Calcular totales generales (ejemplo)
            decimal ventaTotal = analisisClientes.Sum(c => c.VentasMensualesActual.Sum());
            double litrosTotales = ( double ) analisisClientes.Sum(c => c.LitrosMensualesActual.Sum());

            // 3. Serializar la data compleja para que la App solo tenga que leer y deserializar
            string jsonClientes = JsonSerializer.Serialize(analisisClientes);

            await _cacheService.GuardarSnapshotAsync(
                idSucursal,
                ventaTotal,
                litrosTotales,
                jsonClientes
            );

            _logger.LogInformation($"   ✅ Sucursal {idSucursal} actualizada. Venta: {ventaTotal:C2}");
        }

    }
}