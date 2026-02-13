using Dapper; // ¡Importante!

using Microsoft.Data.SqlClient;

using System.Text.Json;

namespace PAR.WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        string cadenaWorker = _configuration.GetConnectionString("Intelisis");

        // 2. Creas el servicio pasándole el string
        var reportesService = new ReportesService(cadenaWorker);
        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while ( !stoppingToken.IsCancellationRequested )
            {
                _logger.LogInformation("⚙️ Iniciando ciclo de procesamiento: {time}", DateTimeOffset.Now);

                try
                {
                    // 1. OBTENER CONFIGURACIÓN
                    string connIntelisis = _configuration.GetConnectionString("Intelisis");
                    string connPar = _configuration.GetConnectionString("ParSystem");
                    var sucursales = _configuration.GetSection("Config:SucursalesIds").Get<int[]>();

                    foreach ( var idSucursal in sucursales )
                    {
                        await ProcesarSucursal(idSucursal, connIntelisis, connPar);
                    }

                    _logger.LogInformation("✅ Ciclo terminado exitosamente.");
                }
                catch ( Exception ex )
                {
                    _logger.LogError(ex, "❌ Error crítico en el Worker");
                }

                // Esperar X minutos (Configurable)
                int minutos = _configuration.GetValue<int>("Config:IntervaloMinutos");
                await Task.Delay(TimeSpan.FromMinutes(minutos), stoppingToken);
            }
        }

        private async Task ProcesarSucursal(int idSucursal, string connIntelisis, string connPar)
        {
            _logger.LogInformation($"   > Procesando Sucursal {idSucursal}...");

            using ( var dbIntelisis = new SqlConnection(connIntelisis) )
            using ( var dbPar = new SqlConnection(connPar) )
            {
                string sqlVentas = @"
                    SELECT 
                        Articulo, 
                        SUM(VentaTotal) as Total, 
                        SUM(Cantidad) as Cantidad 
                    FROM Venta 
                    WHERE Sucursal = @Sucursal 
                      AND FechaEmision BETWEEN @Inicio AND @Fin
                      AND Estatus = 'CONCLUIDO'
                    GROUP BY Articulo";

                var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var finMes = DateTime.Now;

                var datosVentas = await dbIntelisis.QueryAsync(sqlVentas, new { Sucursal = idSucursal, Inicio = inicioMes, Fin = finMes });

                // ====================================================================
                // PASO B: PROCESAR EN MEMORIA (Cálculos C#)
                // ====================================================================
                decimal ventaMes = datosVentas.Sum(x => ( decimal ) x.Total);
                double litrosMes = datosVentas.Sum(x => ( double ) x.Cantidad); // Asumiendo que Cantidad son litros aprox para el ejemplo

                // Generar JSON para Top Productos (Aquí ocurre la magia de la velocidad)
                var topProductos = datosVentas
                    .OrderByDescending(x => x.Total)
                    .Take(10)
                    .Select(x => new { Nombre = x.Articulo, Valor = x.Total });

                string jsonTop = JsonSerializer.Serialize(topProductos);

                // Simulamos gráfica de tendencia (En realidad harías otro query agrupado por día)
                var tendenciaMock = new[] {
                    new { Dia = 1, Venta = ventaMes * 0.1m },
                    new { Dia = 15, Venta = ventaMes * 0.5m },
                    new { Dia = 30, Venta = ventaMes * 0.4m }
                };
                string jsonGrafica = JsonSerializer.Serialize(tendenciaMock);

                // ====================================================================
                // PASO C: GUARDAR EN PAR_System_DB (Cache)
                // ====================================================================
                // Usamos MERGE (o un IF EXISTS UPDATE / ELSE INSERT) para actualizar o crear
                string sqlGuardar = @"
                    MERGE Cache_Dashboard AS target
                    USING (SELECT @IdSucursal AS IdSucursal) AS source
                    ON (target.IdSucursal = source.IdSucursal)
                    WHEN MATCHED THEN
                        UPDATE SET 
                            VentaMes = @VentaMes,
                            LitrosMes = @LitrosMes,
                            JsonTopProductos = @JsonTop,
                            JsonGraficaTendencia = @JsonGrafica,
                            FechaActualizacion = GETDATE()
                    WHEN NOT MATCHED THEN
                        INSERT (IdSucursal, VentaMes, LitrosMes, JsonTopProductos, JsonGraficaTendencia, FechaActualizacion)
                        VALUES (@IdSucursal, @VentaMes, @LitrosMes, @JsonTop, @JsonGrafica, GETDATE());";

                await dbPar.ExecuteAsync(sqlGuardar, new
                {
                    IdSucursal = idSucursal,
                    VentaMes = ventaMes,
                    LitrosMes = litrosMes,
                    JsonTop = jsonTop,
                    JsonGrafica = jsonGrafica
                });
            }
        }
    }
}