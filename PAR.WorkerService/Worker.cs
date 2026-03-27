using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using WPF_PAR.Core.Services; // Acceso al Core
using WPF_PAR.Core.Models;   // Acceso a modelos

namespace PAR.WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 Worker Service INICIADO.");

            while ( !stoppingToken.IsCancellationRequested )
            {
                _logger.LogInformation("⚙️ Iniciando ciclo de actualización: {time}", DateTimeOffset.Now);

                try
                {
                    string? connIntelisis = _configuration.GetConnectionString("Intelisis");
                    string? connPar = _configuration.GetConnectionString("ParSystem");
                    var sucursales = _configuration.GetSection("Config:SucursalesIds").Get<int[]>();

                    if ( string.IsNullOrEmpty(connIntelisis) || string.IsNullOrEmpty(connPar) )
                    {
                        _logger.LogError("❌ Faltan las cadenas de conexión en appsettings.json.");
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }

                    // 1. INSTANCIAMOS LOS SERVICIOS
                    var businessLogic = new BusinessLogicService();
                    var familiaLogic = new FamiliaLogicService(businessLogic);
                    var catalogoService = new CatalogoService(businessLogic);

                    // EL MINERO (Va a Intelisis)
                    var intelisis = new IntelisisDataService(connIntelisis);

                    // EL BODEGUERO (Guarda en Mini PC)
                    var cache = new CacheService(connPar);

                    if ( sucursales != null && sucursales.Length > 0 )
                    {
                        foreach ( var sucId in sucursales )
                        {
                            await ProcesarSucursal(sucId, intelisis, familiaLogic, catalogoService, cache);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No hay sucursales configuradas.");
                    }

                    _logger.LogInformation("✅ Ciclo terminado con éxito.");
                }
                catch ( Exception ex )
                {
                    _logger.LogError(ex, "❌ Error crítico en el ciclo del Worker");
                }

                int minutosEspera = _configuration.GetValue<int>("Config:IntervaloMinutos", 10);
                await Task.Delay(TimeSpan.FromMinutes(minutosEspera), stoppingToken);
            }
        }

        private async Task ProcesarSucursal(
            int sucId,
            IntelisisDataService intelisis,
            FamiliaLogicService familiaLogic,
            CatalogoService catalogoService,
            CacheService cache)
        {
            _logger.LogInformation($"   > Procesando Sucursal {sucId}...");

            try
            {
                var anioActual = DateTime.Now.Year;
                var mesActual = DateTime.Now.Month;
                var inicioMes = new DateTime(anioActual, mesActual, 1);

                // ========================================================
                // A. FAMILIAS Y VENTAS MENSUALES
                // ========================================================
                var ventasRaw = await intelisis.ObtenerVentasRangoAsync(sucId, inicioMes, DateTime.Now);

                foreach ( var v in ventasRaw )
                {
                    var info = catalogoService.ObtenerInfo(v.Articulo);
                    v.Familia = info.FamiliaSimple;
                    v.Linea = info.Linea;
                    v.Descripcion = info.Descripcion;
                    v.LitrosUnitarios = info.Litros;
                }

                // Filtrar Ferretería
                ventasRaw = ventasRaw.Where(x => x.Familia != "FERRETERIA" && !x.Linea.Contains("FERRETERIA", StringComparison.OrdinalIgnoreCase)).ToList();

                var (arqui, espe) = familiaLogic.CalcularResumenGlobal(ventasRaw);
                var todasFamilias = arqui.Concat(espe).ToList();

                await cache.GuardarFamiliasAsync(sucId, todasFamilias);
                await cache.GuardarVentasDetalleAsync(sucId, ventasRaw);
                _logger.LogInformation($"     - Familias y Ventas guardadas ({ventasRaw.Count} items).");

                // ========================================================
                // B. HISTÓRICO ANUAL
                // ========================================================
                var historicoRaw = await intelisis.ObtenerHistoricoAnualPorArticulo(anioActual.ToString(), sucId.ToString());

                foreach ( var item in historicoRaw )
                {
                    var info = catalogoService.ObtenerInfo(item.Articulo);
                    item.Familia = info.FamiliaSimple;
                    item.Linea = info.Linea;
                }

                await cache.GuardarHistoricoAnualAsync(sucId, anioActual, historicoRaw);
                _logger.LogInformation($"     - Histórico Anual guardado.");

                // ========================================================
                // C. DASHBOARD TENDENCIA
                // ========================================================
                // Nota: Asumo que en tu viejo servicio tenías un parámetro bool agruparPorMes
                var tendenciaMes = await intelisis.ObtenerTendenciaGrafica(sucId, inicioMes, DateTime.Now, agruparPorMes: false);
                await cache.GuardarDashboardAsync(sucId, tendenciaMes);
                _logger.LogInformation($"     - Tendencia Dashboard guardada.");

                // ========================================================
                // D. CLIENTES
                // ========================================================
                var clientesBase = await intelisis.ObtenerDatosBaseClientes(anioActual, sucId); // <-- Cambié el nombre al que le pusiste en tu clase
                await cache.GuardarClientesBaseAsync(sucId, anioActual, clientesBase);
                _logger.LogInformation($"     - Top Clientes guardado ({clientesBase.Count} clientes).");

                // Fechas para la variación (Ej. del 1 de enero al día de hoy)
                DateTime fechaInicioVariacion = new DateTime(anioActual, 1, 1);
                DateTime fechaFinVariacion = DateTime.Now;

                var topClientes = clientesBase.Take(10).ToList();
                foreach ( var cliente in topClientes )
                {
                    if ( string.IsNullOrEmpty(cliente.Cliente) ) continue;

                    var kpi = await intelisis.ObtenerKpisCliente(cliente.Nombre, anioActual, sucId); // OJO: Tu query usa c.Nombre = @NombreCliente

                    // AHORA SÍ LE PASAMOS LOS DATETIME
                    var variacion = await intelisis.ObtenerVariacionProductos(cliente.Nombre, fechaInicioVariacion, fechaFinVariacion, sucId);

                    await cache.GuardarClienteDetalleAsync(sucId, anioActual, cliente.Cliente, kpi, variacion);
                }
                _logger.LogInformation($"     - Detalle de KPIs y Variaciones del Top 10 guardado.");
            }
            catch ( Exception ex )
            {
                _logger.LogError($"   ❌ Error procesando sucursal {sucId}: {ex.Message}");
            }
        }
    }
}