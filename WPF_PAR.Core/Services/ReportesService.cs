using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services
{
    public class ReportesService
    {
        private readonly SqlHelper _sqlHelper;

        public ReportesService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
        }

        // ============================================================
        // 1. EL QUE USA EL DASHBOARD (VENTAS DEL MES)
        // ============================================================
        public async Task<List<VentaReporteModel>> ObtenerVentasDetalleAsync(int sucursalId)
        {
            string query = @"
                SELECT JsonVentas 
                FROM Cache_VentasDetalle 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio AND Mes = @Mes";

            var parametros = new
            {
                Sucursal = sucursalId,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month
            };

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            return string.IsNullOrEmpty(json)
                ? new List<VentaReporteModel>()
                : JsonConvert.DeserializeObject<List<VentaReporteModel>>(json);
        }

        // ============================================================
        // 2. EL QUE USA EL DASHBOARD (GRÁFICA DE TENDENCIA)
        // ============================================================
        public async Task<List<GraficoPuntoModel>> ObtenerTendenciaGrafica(int sucursalId, DateTime inicio, DateTime fin, bool agruparPorMes)
        {
            string query = @"
                SELECT JsonGraficaTendencia 
                FROM Cache_Dashboard 
                WHERE IdSucursal = @Sucursal";

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, new { Sucursal = sucursalId });

            return string.IsNullOrEmpty(json)
                ? new List<GraficoPuntoModel>()
                : JsonConvert.DeserializeObject<List<GraficoPuntoModel>>(json);
        }

        // ============================================================
        // 3. EL QUE USA EL PDF Y REPORTES (HISTÓRICO)
        // ============================================================
        public async Task<List<VentaReporteModel>> ObtenerVentasRangoAsync(int sucursalId, DateTime inicio, DateTime fin)
        {
            // 1. Si el rango solicitado es exactamente el mes actual, leemos la tabla rápida
            if ( inicio.Year == DateTime.Now.Year && inicio.Month == DateTime.Now.Month && fin.Month == DateTime.Now.Month )
            {
                return await ObtenerVentasDetalleAsync(sucursalId);
            }

            // 2. Si el rango abarca otros meses, leemos del Histórico Anual
            string query = @"
                SELECT JsonDatos 
                FROM Cache_HistoricoAnual 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio";

            var parametros = new { Sucursal = sucursalId, Anio = inicio.Year };
            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            if ( string.IsNullOrEmpty(json) )
                return new List<VentaReporteModel>();

            var ventasAnuales = JsonConvert.DeserializeObject<List<VentaReporteModel>>(json);

            // 3. Filtramos en memoria para devolver solo las fechas solicitadas
            return ventasAnuales
                .Where(v => v.FechaEmision.Date >= inicio.Date && v.FechaEmision.Date <= fin.Date)
                .ToList();
        }

        public async Task<List<VentaReporteModel>> ObtenerHistoricoAnualPorArticulo(string ejercicio, string sucursal)
        {
            string query = @"
                SELECT JsonDatos 
                FROM Cache_HistoricoAnual 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio";

            var parametros = new { Sucursal = int.Parse(sucursal), Anio = int.Parse(ejercicio) };

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            return string.IsNullOrEmpty(json)
                ? new List<VentaReporteModel>()
                : JsonConvert.DeserializeObject<List<VentaReporteModel>>(json);
        }

        // ============================================================
        // 4. DETALLES DE CLIENTES (KPIs Y VARIACIÓN)
        // ============================================================
        public async Task<KpiClienteModel> ObtenerKpisCliente(string nombreCliente, int anio, int sucursalId)
        {
            string query = @"
                SELECT JsonKpi 
                FROM Cache_Clientes_Detalle 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio AND Cliente = @Nombre";

            var parametros = new { Sucursal = sucursalId, Anio = anio, Nombre = nombreCliente };

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            return string.IsNullOrEmpty(json)
                ? new KpiClienteModel()
                : JsonConvert.DeserializeObject<KpiClienteModel>(json);
        }

        public async Task<List<ProductoAnalisisModel>> ObtenerVariacionProductosCliente(string nombreCliente, DateTime inicio, DateTime fin, int sucursalId)
        {
            string query = @"
                SELECT JsonVariacion 
                FROM Cache_Clientes_Detalle 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio AND Cliente = @Nombre";

            var parametros = new
            {
                Sucursal = sucursalId,
                Anio = inicio.Year,
                Nombre = nombreCliente
            };

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            return string.IsNullOrEmpty(json)
                ? new List<ProductoAnalisisModel>()
                : JsonConvert.DeserializeObject<List<ProductoAnalisisModel>>(json);
        }
    }
}