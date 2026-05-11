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
        public async Task<List<VentaReporteModel>> ObtenerVentasDetalleAsync(int sucursalId, DateTime fechaInicio, DateTime fechaFin)
        {
            string query = @"
                SELECT JsonVentas 
                FROM Cache_VentasDetalle 
                WHERE IdSucursal = @Sucursal 
                    AND (Anio > @AnioInicio OR (Anio = @AnioInicio AND Mes >= @MesInicio))
                    AND (Anio < @AnioFin OR (Anio = @AnioFin AND Mes <= @MesFin))";

            var parametros = new
            {
                Sucursal = sucursalId,
                AnioInicio = fechaInicio.Year,
                MesInicio = fechaInicio.Month,
                AnioFin = fechaFin.Year,
                MesFin = fechaFin.Month
            };

            var listaJsons = await _sqlHelper.QueryAsync<string>(query, parametros);

            var listaVentas = new List<VentaReporteModel>();

            if (listaJsons == null) return listaVentas;

            foreach (var json in listaJsons)
            {
                if (!string.IsNullOrEmpty(json))
                {
                    var ventasDelMes = JsonConvert.DeserializeObject<List<VentaReporteModel>>(json);
                    if (ventasDelMes != null)
                    {
                        listaVentas.AddRange(ventasDelMes);
                    }
                }
            }

            return listaVentas;
        }

        public async Task<List<GraficoPuntoModel>> ObtenerTendenciaGrafica(int sucursalId, string periodoSeleccionado)
        {
            string query = @"SELECT JsonGraficaTendencia FROM Cache_Dashboard WHERE IdSucursal = @Sucursal";
            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, new { Sucursal = sucursalId });

            if (string.IsNullOrEmpty(json)) return new List<GraficoPuntoModel>();

            var dict = JsonConvert.DeserializeObject<Dictionary<string, List<GraficoPuntoModel>>?>(json);

            if (periodoSeleccionado == "Hoy" && dict.TryGetValue("Hoy", out List<GraficoPuntoModel>? value)) return value;
            if (periodoSeleccionado == "Este Año" && dict.TryGetValue("Anio", out List<GraficoPuntoModel>? value1)) return value1;

            return dict.TryGetValue("Mes", out List<GraficoPuntoModel>? value2) ? value2 : new List<GraficoPuntoModel>();
        }
        public async Task<List<VentaReporteModel>?> ObtenerVentasRangoAsync(int sucursalId, DateTime inicio, DateTime fin)
        {
            if ( inicio.Year == DateTime.Now.Year && inicio.Month == DateTime.Now.Month && fin.Month == DateTime.Now.Month )
            {
                return await ObtenerVentasDetalleAsync(sucursalId, inicio, fin);
            }

            string query = @"
                SELECT JsonDatos 
                FROM Cache_HistoricoAnual 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio";

            var parametros = new { Sucursal = sucursalId, Anio = inicio.Year };
            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            if ( string.IsNullOrEmpty(json) )
                return new List<VentaReporteModel>();

            var ventasAnuales = JsonConvert.DeserializeObject<List<VentaReporteModel>>(json);

            return ventasAnuales
                .Where(v => v.FechaEmision.Date >= inicio.Date && v.FechaEmision.Date <= fin.Date)
                .ToList();
        }
    }
}