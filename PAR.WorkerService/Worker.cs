using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WPF_PAR.Core.Services;
using WPF_PAR.Core.Models;

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

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("⚙️ Iniciando ciclo de actualización: {time}", DateTimeOffset.Now);

                try
                {
                    string? connIntelisis = _configuration.GetConnectionString("Intelisis");
                    string? connPar = _configuration.GetConnectionString("ParSystem");
                    var sucursales = _configuration.GetSection("Config:SucursalesIds").Get<int[]>();

                    if (string.IsNullOrEmpty(connIntelisis) || string.IsNullOrEmpty(connPar))
                    {
                        _logger.LogError("❌ Faltan las cadenas de conexión en appsettings.json.");
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }

                    // 1. INSTANCIAMOS LOS SERVICIOS
                    var businessLogic = new BusinessLogicService();

                    // ========================================================
                    // BLINDADJE 1: Le inyectamos la conexión para que sí lea SQL
                    // ========================================================
                    businessLogic.SetParSystemConnectionString(connPar);

                    var familiaLogic = new FamiliaLogicService(businessLogic);
                    var catalogoService = new CatalogoService(businessLogic);

                    await catalogoService.CargarCatalogoSqlAsync();

                    var intelisis = new IntelisisDataService(connIntelisis);
                    var cache = new CacheService(connPar);

                    if (sucursales != null && sucursales.Length > 0)
                    {
                        foreach (var sucId in sucursales)
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
                catch (Exception ex)
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
                // BLINDAJE 2: FUNCIÓN MAESTRA DEL ESCÁNER (Evita omitir columnas o listas)
                // ========================================================
                void EnriquecerConCatalogo(IEnumerable<VentaReporteModel> lista)
                {
                    if (lista == null) return;
                    foreach (var v in lista)
                    {
                        // El .Trim() salva la vida si Intelisis manda espacios vacíos
                        string codigoLimpio = v.Articulo?.Trim() ?? "";
                        var info = catalogoService.ObtenerInfo(codigoLimpio);

                        v.Familia = info.Familia;
                        v.Linea = info.Linea;
                        v.Descripcion = info.Descripcion;
                        v.LitrosUnitarios = (decimal)info.Litros;
                    }
                }
                void EnriquecerVariacion(IEnumerable<ProductoAnalisisModel> lista)
                {
                    if (lista == null) return;
                    foreach (var v in lista)
                    {
                        string codigoLimpio = v.Articulo?.Trim() ?? "";
                        var info = catalogoService.ObtenerInfo(codigoLimpio);

                        v.Familia = info.Familia;
                        v.Linea = info.Linea;
                        v.Descripcion = info.Descripcion;
                        v.LitrosUnitarios = (decimal)info.Litros;
                    }
                }

                // ========================================================
                // A. FAMILIAS Y VENTAS MENSUALES
                // ========================================================
                var ventasRaw = await intelisis.ObtenerVentasRangoAsync(sucId, inicioMes, DateTime.Now);

                // Pasamos las ventas por el escáner
                EnriquecerConCatalogo(ventasRaw);

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

                // Pasamos el histórico por el escáner (¡Antes se te había olvidado ponerle litros aquí!)
                EnriquecerConCatalogo(historicoRaw);

                await cache.GuardarHistoricoAnualAsync(sucId, anioActual, historicoRaw);
                _logger.LogInformation($"     - Histórico Anual guardado.");

                // ========================================================
                // C. DASHBOARD TENDENCIA
                // ========================================================
                var dictTendencias = new Dictionary<string, List<GraficoPuntoModel>>();

                var ventasHoy = ventasRaw.Where(v => v.FechaEmision.Date == DateTime.Now.Date);
                dictTendencias["Hoy"] = ventasHoy
                    .GroupBy(v => v.FechaEmision.Hour)
                    .Select(g => new GraficoPuntoModel
                    {
                        Indice = g.Key,
                        Total = g.Sum(v => v.LitrosTotales) // BLINDAJE 3: Usamos LitrosTotales
                    }).ToList();

                dictTendencias["Mes"] = ventasRaw
                    .GroupBy(v => v.FechaEmision.Day)
                    .Select(g => new GraficoPuntoModel
                    {
                        Indice = g.Key,
                        Total = g.Sum(v => v.LitrosTotales)
                    }).ToList();

                dictTendencias["Anio"] = historicoRaw
                    .GroupBy(v => v.FechaEmision.Month)
                    .Select(g => new GraficoPuntoModel
                    {
                        Indice = g.Key,
                        Total = g.Sum(v => v.LitrosTotales)
                    }).ToList();

                await cache.GuardarDashboardAsync(sucId, dictTendencias);
                _logger.LogInformation($"     - Tendencia Dashboard guardada con Litros Reales.");

                // ========================================================
                // D. CLIENTES
                // ========================================================
                var clientesBase = await intelisis.ObtenerDatosBaseClientes(anioActual, sucId);
                await cache.GuardarClientesBaseAsync(sucId, anioActual, clientesBase);
                _logger.LogInformation($"     - Top Clientes guardado ({clientesBase.Count} clientes).");

                DateTime fechaInicioVariacion = new DateTime(anioActual, 1, 1);
                DateTime fechaFinVariacion = DateTime.Now;

                var topClientes = clientesBase.Take(10).ToList();
                foreach (var cliente in topClientes)
                {
                    if (string.IsNullOrEmpty(cliente.Cliente)) continue;

                    var kpi = await intelisis.ObtenerKpisCliente(cliente.Nombre, anioActual, sucId);
                    var variacion = await intelisis.ObtenerVariacionProductos(cliente.Nombre, fechaInicioVariacion, fechaFinVariacion, sucId);

                    // BLINDAJE 4: Faltaba escanear la variación de los clientes
                    EnriquecerVariacion(variacion);

                    await cache.GuardarClienteDetalleAsync(sucId, anioActual, cliente.Cliente, kpi, variacion);
                }
                _logger.LogInformation($"     - Detalle de KPIs y Variaciones del Top 10 guardado.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"   ❌ Error procesando sucursal {sucId}: {ex.Message}");
            }
        }
    }
}