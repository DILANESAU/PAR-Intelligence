using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                    // 1. LEER CONFIGURACIÓN (Desde appsettings.json)
                    // Asegúrate que en appsettings.json tengas: 
                    // "ConnectionStrings": { "Intelisis": "...", "ParSystem": "..." }
                    string connIntelisis = _configuration.GetConnectionString("Intelisis");
                    string connPar = _configuration.GetConnectionString("ParSystem");

                    // Asegúrate que en appsettings.json tengas: "Config": { "SucursalesIds": [1, 2, 3] }
                    var sucursales = _configuration.GetSection("Config:SucursalesIds").Get<int[]>();

                    if ( string.IsNullOrEmpty(connIntelisis) || string.IsNullOrEmpty(connPar) )
                    {
                        _logger.LogError("❌ No se encontraron las cadenas de conexión en appsettings.json.");
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }

                    // 2. INSTANCIAR SERVICIOS (Inyección Manual)
                    // Creamos BusinessLogic primero porque FamiliaLogic lo necesita
                    var businessLogic = new BusinessLogicService();

                    var reportesService = new ReportesService(connIntelisis);
                    var clientesService = new ClientesService(connIntelisis);
                    var familiaLogic = new FamiliaLogicService(businessLogic);

                    // CacheService usa la base intermedia (PAR System)
                    var cacheService = new CacheService(connPar);

                    // 3. PROCESAR CADA SUCURSAL
                    if ( sucursales != null && sucursales.Length > 0 )
                    {
                        foreach ( var sucId in sucursales )
                        {
                            await ProcesarSucursal(sucId, reportesService, clientesService, familiaLogic, cacheService);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No hay sucursales configuradas en 'Config:SucursalesIds'.");
                    }

                    _logger.LogInformation("✅ Ciclo terminado con éxito.");
                }
                catch ( Exception ex )
                {
                    _logger.LogError(ex, "❌ Error crítico en el ciclo del Worker");
                }

                // Esperar N minutos antes de la siguiente vuelta
                int minutosEspera = _configuration.GetValue<int>("Config:IntervaloMinutos", 10); // Default 10 min
                await Task.Delay(TimeSpan.FromMinutes(minutosEspera), stoppingToken);
            }
        }

        private async Task ProcesarSucursal(
            int sucId,
            ReportesService reportes,
            ClientesService clientes,
            FamiliaLogicService familiaLogic,
            CacheService cache)
        {
            _logger.LogInformation($"   > Procesando Sucursal {sucId}...");

            try
            {
                // ========================================================
                // A. FAMILIAS (Ventas por Familia)
                // ========================================================
                var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                // 1. Traer datos crudos de Intelisis
                var ventasRaw = await reportes.ObtenerVentasBrutasRango(sucId, inicioMes, DateTime.Now);

                // 2. Aplicar lógica de negocio
                // NOTA: Asegúrate que VentaReporteModel tenga las propiedades Familia/Linea llenas.
                // Si vienen vacías de SQL, necesitas llenarlas aquí usando CatalogoService o similar.
                // Como FamiliaLogicService asume que ya tienen familia, quizás necesites un CatalogoService aquí también.

                // --- INICIO PARCHE CATÁLOGO ---
                // Si tus ventasRaw no traen Familia/Linea desde el SQL, descomenta esto:
                /*
                var catalogo = new CatalogoService(new BusinessLogicService());
                foreach(var v in ventasRaw) {
                    var info = catalogo.ObtenerInfo(v.Articulo);
                    v.Familia = info.FamiliaSimple;
                    v.Linea = info.Linea;
                }
                */
                // --- FIN PARCHE CATÁLOGO ---

                var (arqui, espe) = familiaLogic.CalcularResumenGlobal(ventasRaw);
                var todasFamilias = arqui.Concat(espe).ToList();

                // 3. Guardar en Cache (Base intermedia)
                // Asegúrate que CacheService tenga este método implementado
                // await cache.GuardarFamiliasAsync(sucId, todasFamilias);
                _logger.LogInformation($"     - Familias procesadas ({todasFamilias.Count} registros)");

                // ========================================================
                // B. CLIENTES (Top Clientes)
                // ========================================================

                // NOTA: ClientesService.ObtenerTopClientes NO EXISTE en el código que revisamos antes.
                // Tienes ObtenerDatosBase o ObtenerKpisCliente.
                // Debes implementar ObtenerTopClientes en ClientesService o usar una lógica aquí.

                // Ejemplo Simulado:
                /*
                var topClientes = await clientes.ObtenerDatosBase(DateTime.Now.Year, sucId); 
                // Filtrar o procesar topClientes si es necesario...
                await cache.GuardarClientesAsync(sucId, topClientes);
                */

                _logger.LogInformation($"     - Clientes procesados.");
            }
            catch ( Exception ex )
            {
                _logger.LogError($"   ❌ Error procesando sucursal {sucId}: {ex.Message}");
            }
        }
    }
}