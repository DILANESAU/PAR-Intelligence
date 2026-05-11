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


                //await LlenarHistorialVentasYFamiliasAsync(sucId, intelisis, catalogoService, familiaLogic, cache);
                // ========================================================
                // BLINDAJE 2: FUNCIÓN MAESTRA DEL ESCÁNER (Evita omitir columnas o listas)
                // ========================================================
                void EnriquecerConCatalogo(IEnumerable<VentaReporteModel> lista)
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

                // 🚨 EL RADAR: Imprime en la consola negra qué bajó de SQL
                Console.WriteLine($"\n[RADAR] SQL devolvió {ventasRaw?.Count ?? 0} ventas en total para este mes.");
                if (ventasRaw != null && ventasRaw.Count > 0)
                {
                    Console.WriteLine($"[RADAR] Ejemplo Venta 1: Fecha='{ventasRaw[0].FechaEmision}' | Articulo='{ventasRaw[0].Articulo}' | Cantidad={ventasRaw[0].Cantidad}");
                }
                Console.WriteLine(""); // Salto de línea para que se vea limpio
                // Pasamos las ventas por el escáner
                EnriquecerConCatalogo(ventasRaw);

                // Filtrar Ferretería
                ventasRaw = ventasRaw.Where(x => x.Familia != "FERRETERIA" && !x.Linea.Contains("FERRETERIA", StringComparison.OrdinalIgnoreCase)).ToList();

                var (arqui, espe) = familiaLogic.CalcularResumenGlobal(ventasRaw);
                var todasFamilias = arqui.Concat(espe).ToList();

                await cache.GuardarFamiliasAsync(sucId, todasFamilias);
                await cache.GuardarVentasDetalleAsync(sucId, anioActual, mesActual, ventasRaw);
                _logger.LogInformation($"     - Familias y Ventas guardadas ({ventasRaw.Count} items).");

                _logger.LogInformation($"     > Descargando 5 años de historia de familias para la sucursal {sucId}...");

                _logger.LogInformation($"     > Descargando 5 años crudos para la sucursal {sucId}...");

                // 1. Descargamos el pasado
                var historialCrudo = await intelisis.DescargarHistorialCrudoAsync(sucId);

                if (historialCrudo != null && historialCrudo.Any())
                {
                    // 2. 🟢 ¡LA MAGIA DE LA REUTILIZACIÓN! Pasamos los 5 años por tu túnel de lavado
                    EnriquecerConCatalogo(historialCrudo);

                    // Opcional: Filtramos ferretería como lo haces con las ventas del mes
                    historialCrudo = historialCrudo.Where(x => x.Familia != "FERRETERIA" && !(x.Linea?.Contains("FERRETERIA", StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

                    var datosAgrupadosPorMes = historialCrudo
                        .GroupBy(x => new { Anio = x.FechaEmision.Year, Mes = x.FechaEmision.Month })
                        .ToList();

                    foreach (var grupoMes in datosAgrupadosPorMes)
                    {
                        var familiasDelMes = grupoMes
                            .GroupBy(x => x.Familia ?? "OTROS")
                            .Select(g => new FamiliaResumenModel
                            {
                                NombreFamilia = g.Key,
                                VentaTotal = (decimal)g.Sum(v => v.TotalVenta),
                                LitrosTotal = (double)g.Sum(v => v.LitrosTotales)
                            }).ToList();

                        await cache.GuardarFamiliasHistoricoAsync(sucId, grupoMes.Key.Anio, grupoMes.Key.Mes, familiasDelMes);
                    }

                    _logger.LogInformation($"     - Histórico de familias guardado, limpio y procesado.");
                }
                var historicoRaw = await intelisis.ObtenerHistoricoAnualPorArticulo(anioActual.ToString(), sucId.ToString());

                EnriquecerConCatalogo(historicoRaw);

                await cache.GuardarHistoricoAnualAsync(sucId, anioActual, historicoRaw);
                _logger.LogInformation($"     - Histórico Anual guardado.");

                var dictTendencias = new Dictionary<string, List<GraficoPuntoModel>>();

                var ventasHoy = ventasRaw.Where(v => v.FechaEmision.Date == DateTime.Now.Date).ToList();
                dictTendencias["Hoy"] = Enumerable.Range(0, 24).Select(hora => new GraficoPuntoModel
                {
                    Indice = hora,
                    Total = ventasHoy.Where(v => v.FechaEmision.Hour == hora).Sum(v => v.LitrosTotales)
                }).ToList();

                int diasDelMes = DateTime.DaysInMonth(anioActual, mesActual);
                dictTendencias["Mes"] = Enumerable.Range(1, diasDelMes).Select(dia => new GraficoPuntoModel
                {
                    Indice = dia,
                    Total = ventasRaw.Where(v => v.FechaEmision.Day == dia).Sum(v => v.LitrosTotales)
                }).ToList();

                dictTendencias["Anio"] = Enumerable.Range(1, 12).Select(mes => new GraficoPuntoModel
                {
                    Indice = mes,
                    Total = historicoRaw.Where(v => v.FechaEmision.Month == mes).Sum(v => v.LitrosTotales)
                }).ToList();

                await cache.GuardarDashboardAsync(sucId, dictTendencias);
                _logger.LogInformation($"     - Tendencia Dashboard guardada con Gráficas Continuas.");

                var clientesBase = await intelisis.ObtenerDatosBaseClientes(anioActual, sucId);
                await cache.GuardarClientesBaseAsync(sucId, anioActual, clientesBase);
                _logger.LogInformation($"     - Top Clientes guardado ({clientesBase.Count} clientes).");

                DateTime fechaInicioVariacion = new DateTime(anioActual, 1, 1);
                DateTime fechaFinVariacion = DateTime.Now;

                var topClientes = clientesBase.Take(100).ToList();
                foreach (var cliente in topClientes)
                {
                    if (string.IsNullOrEmpty(cliente.Cliente)) continue;

                    try
                    {
                        var kpi = await intelisis.ObtenerKpisCliente(cliente.Cliente, anioActual, sucId);
                        var variacion = await intelisis.ObtenerVariacionProductos(cliente.Cliente, fechaInicioVariacion, fechaFinVariacion, sucId);

                        EnriquecerVariacion(variacion);

                        await cache.GuardarClienteDetalleAsync(sucId, anioActual, cliente.Cliente, cliente.Nombre, kpi, variacion);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"     ⚠️ Error aislando al cliente {cliente.Cliente} ({cliente.Nombre}): {ex.Message}");
                    }
                }
                _logger.LogInformation($"     - Detalle de KPIs y Variaciones del Top 100 guardado.");
                _logger.LogInformation($"     > Descargando 5 años de historia para la sucursal {sucId}...");

                var historialMasivo = await intelisis.DescargarHistorialTodosLosClientes(sucId);

                if (historialMasivo != null && historialMasivo.Any())
                {
                    await cache.GuardarHistorialClientesMasivoAsync(sucId, historialMasivo);
                    _logger.LogInformation($"     - Histórico de 5 años guardado ({historialMasivo.Count} registros).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"   ❌ Error procesando sucursal {sucId}: {ex.Message}");
            }
        }

        // 🟢 Nota: Agregamos FamiliaLogicService a los parámetros
        private async Task LlenarHistorialVentasYFamiliasAsync(
            int sucId,
            IntelisisDataService intelisis,
            CatalogoService catalogoService,
            FamiliaLogicService familiaLogic,
            CacheService cache)
        {
            _logger.LogInformation($"[VIAJE EN EL TIEMPO] Iniciando descarga de 2 años (Ventas y Familias) para sucursal {sucId}...");

            // Viajamos 24 meses al pasado
            DateTime fechaIteracion = new DateTime(DateTime.Now.Year - 2, DateTime.Now.Month, 1);
            DateTime fechaFin = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            while (fechaIteracion < fechaFin)
            {
                int anio = fechaIteracion.Year;
                int mes = fechaIteracion.Month;
                DateTime inicioMes = fechaIteracion;
                DateTime finMes = fechaIteracion.AddMonths(1).AddDays(-1);

                _logger.LogInformation($"Descargando detalle de: {mes}/{anio}...");

                var ventasMes = await intelisis.ObtenerVentasRangoAsync(sucId, inicioMes, finMes);

                if (ventasMes != null && ventasMes.Count > 0)
                {
                    // 1. Enriquecer con catálogo
                    foreach (var v in ventasMes)
                    {
                        string codigoLimpio = v.Articulo?.Trim() ?? "";
                        var info = catalogoService.ObtenerInfo(codigoLimpio);
                        v.Familia = info.Familia;
                        v.Linea = info.Linea;
                        v.Descripcion = info.Descripcion;
                        v.LitrosUnitarios = (decimal)info.Litros;
                    }

                    // 2. Filtrar Ferretería
                    var ventasLimpias = ventasMes.Where(x => x.Familia != "FERRETERIA" && !(x.Linea?.Contains("FERRETERIA", StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

                    // 3. 🟢 GUARDAR VENTAS DETALLE (Para KPIs del Dashboard)
                    await cache.GuardarVentasDetalleAsync(sucId, anio, mes, ventasLimpias);

                    // 4. 🟢 CALCULAR Y GUARDAR FAMILIAS (Para la vista de Familias)
                    var (arqui, espe) = familiaLogic.CalcularResumenGlobal(ventasLimpias);
                    var todasFamilias = arqui.Concat(espe).ToList();
                    await cache.GuardarFamiliasHistoricoAsync(sucId, anio, mes, todasFamilias);

                    _logger.LogInformation($"✅ Mes {mes}/{anio} guardado: {ventasLimpias.Count} tickets y {todasFamilias.Count} familias.");
                }
                else
                {
                    _logger.LogWarning($"⚠️ No se encontraron ventas en {mes}/{anio}.");
                }

                // Avanzamos al siguiente mes
                fechaIteracion = fechaIteracion.AddMonths(1);
            }

            _logger.LogInformation($"[VIAJE EN EL TIEMPO] ¡Descarga histórica completada con éxito!");
        }
    }
}