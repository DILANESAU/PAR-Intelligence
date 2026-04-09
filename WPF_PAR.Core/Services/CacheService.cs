using Dapper;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services
{
    public class CacheService
    {
        private readonly SqlHelper _sqlHelper;

        public CacheService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
        }

        // ========================================================
        // FAMILIAS
        // ========================================================
        public async Task GuardarFamiliasAsync(int idSucursal, List<FamiliaResumenModel> datos)
        {
            string json = JsonConvert.SerializeObject(datos);
            string sql = @"
                MERGE Cache_Familias AS target
                USING (SELECT @IdSucursal AS IdSucursal, @Anio AS Anio, @Mes AS Mes) AS source
                ON (target.IdSucursal = source.IdSucursal AND target.Anio = source.Anio AND target.Mes = source.Mes)
                WHEN MATCHED THEN
                    UPDATE SET JsonResumen = @Json
                WHEN NOT MATCHED THEN
                    INSERT (IdSucursal, Anio, Mes, JsonResumen)
                    VALUES (@IdSucursal, @Anio, @Mes, @Json);";

            await _sqlHelper.ExecuteAsync(sql, new
            {
                IdSucursal = idSucursal,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month,
                Json = json
            });
        }

        public async Task<List<FamiliaResumenModel>> ObtenerFamiliasAsync(int idSucursal)
        {
            string sql = @"
        SELECT JsonResumen 
        FROM Cache_Familias 
        WHERE IdSucursal = @IdSucursal 
          AND Anio = @Anio 
          AND Mes = @Mes";

            var jsonResult = await _sqlHelper.QueryFirstOrDefaultAsync<string>(sql, new
            {
                IdSucursal = idSucursal,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month
            });

            if ( string.IsNullOrEmpty(jsonResult) )
                return new List<FamiliaResumenModel>();

            return JsonConvert.DeserializeObject<List<FamiliaResumenModel>>(jsonResult);
        }

        // ========================================================
        // VENTAS DETALLE (Gráficas)
        // ========================================================
        public async Task GuardarVentasDetalleAsync(int idSucursal, List<VentaReporteModel> ventas)
        {
            string json = JsonConvert.SerializeObject(ventas);
            string sql = @"
                MERGE Cache_VentasDetalle AS target
                USING (SELECT @IdSucursal AS IdSucursal, @Anio AS Anio, @Mes AS Mes) AS source
                ON (target.IdSucursal = source.IdSucursal AND target.Anio = source.Anio AND target.Mes = source.Mes)
                WHEN MATCHED THEN
                    UPDATE SET JsonVentas = @Json
                WHEN NOT MATCHED THEN
                    INSERT (IdSucursal, Anio, Mes, JsonVentas)
                    VALUES (@IdSucursal, @Anio, @Mes, @Json);";

            await _sqlHelper.ExecuteAsync(sql, new
            {
                IdSucursal = idSucursal,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month,
                Json = json
            });
        }

        public async Task<List<VentaReporteModel>> ObtenerVentasDetalleAsync(int idSucursal)
        {
            string sql = @"
                SELECT JsonVentas 
                FROM Cache_VentasDetalle 
                WHERE IdSucursal = @IdSucursal AND Anio = @Anio AND Mes = @Mes";

            var jsonResult = await _sqlHelper.QueryFirstOrDefaultAsync<string>(sql, new
            {
                IdSucursal = idSucursal,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month
            });

            if ( string.IsNullOrEmpty(jsonResult) )
                return new List<VentaReporteModel>();

            return JsonConvert.DeserializeObject<List<VentaReporteModel>>(jsonResult);
        }

        // ========================================================
        // HISTÓRICO ANUAL
        // ========================================================
        public async Task GuardarHistoricoAnualAsync(int idSucursal, int anio, List<VentaReporteModel> datos)
        {
            string json = JsonConvert.SerializeObject(datos);
            string sql = @"
                MERGE Cache_HistoricoAnual AS target
                USING (SELECT @IdSucursal AS IdSucursal, @Anio AS Anio) AS source
                ON (target.IdSucursal = source.IdSucursal AND target.Anio = source.Anio)
                WHEN MATCHED THEN
                    UPDATE SET JsonDatos = @Json
                WHEN NOT MATCHED THEN
                    INSERT (IdSucursal, Anio, JsonDatos)
                    VALUES (@IdSucursal, @Anio, @Json);";

            await _sqlHelper.ExecuteAsync(sql, new { IdSucursal = idSucursal, Anio = anio, Json = json });
        }

        public async Task<List<VentaReporteModel>> ObtenerHistoricoAnualAsync(int idSucursal, int anio)
        {
            string sql = @"
                SELECT JsonDatos 
                FROM Cache_HistoricoAnual 
                WHERE IdSucursal = @IdSucursal AND Anio = @Anio";

            var jsonResult = await _sqlHelper.QueryFirstOrDefaultAsync<string>(sql, new { IdSucursal = idSucursal, Anio = anio });

            if ( string.IsNullOrEmpty(jsonResult) )
                return new List<VentaReporteModel>();

            return JsonConvert.DeserializeObject<List<VentaReporteModel>>(jsonResult);
        }

        // ========================================================
        // DASHBOARD TENDENCIA
        // ========================================================
        public async Task GuardarDashboardAsync(int idSucursal, object tendencia)
        {
            // El resto del código de este método se queda IGUAL.
            string json = JsonConvert.SerializeObject(tendencia);
            string sql = @"
        IF EXISTS (SELECT 1 FROM Cache_Dashboard WHERE IdSucursal = @IdSucursal)
            UPDATE Cache_Dashboard SET JsonGraficaTendencia = @Json WHERE IdSucursal = @IdSucursal
        ELSE
            INSERT INTO Cache_Dashboard (IdSucursal, JsonGraficaTendencia) VALUES (@IdSucursal, @Json)";

            await _sqlHelper.ExecuteAsync(sql, new { IdSucursal = idSucursal, Json = json });
        }

        // ========================================================
        // CLIENTES BASE Y DETALLES
        // ========================================================
        public async Task GuardarClientesBaseAsync(int idSucursal, int anio, List<ClienteAnalisisModel> clientes)
        {
            string json = JsonConvert.SerializeObject(clientes);
            string sql = @"
                IF EXISTS (SELECT 1 FROM Cache_Clientes WHERE IdSucursal = @IdSucursal AND Anio = @Anio)
                    UPDATE Cache_Clientes SET JsonDatosBase = @Json WHERE IdSucursal = @IdSucursal AND Anio = @Anio
                ELSE
                    INSERT INTO Cache_Clientes (IdSucursal, Anio, JsonDatosBase) VALUES (@IdSucursal, @Anio, @Json)";

            await _sqlHelper.ExecuteAsync(sql, new { IdSucursal = idSucursal, Anio = anio, Json = json });
        }

        public async Task GuardarClienteDetalleAsync(int idSucursal, int anio, string cliente, KpiClienteModel kpi, List<ProductoAnalisisModel> variacion)
        {
            string jsonKpi = JsonConvert.SerializeObject(kpi);
            string jsonVariacion = JsonConvert.SerializeObject(variacion);

            string sql = @"
                IF EXISTS (SELECT 1 FROM Cache_Clientes_Detalle WHERE IdSucursal = @IdSucursal AND Anio = @Anio AND Cliente = @Cliente)
                    UPDATE Cache_Clientes_Detalle 
                    SET JsonKpi = @JsonKpi, JsonVariacion = @JsonVariacion 
                    WHERE IdSucursal = @IdSucursal AND Anio = @Anio AND Cliente = @Cliente
                ELSE
                    INSERT INTO Cache_Clientes_Detalle (IdSucursal, Anio, Cliente, JsonKpi, JsonVariacion) 
                    VALUES (@IdSucursal, @Anio, @Cliente, @JsonKpi, @JsonVariacion)";

            await _sqlHelper.ExecuteAsync(sql, new
            {
                IdSucursal = idSucursal,
                Anio = anio,
                Cliente = cliente,
                JsonKpi = jsonKpi,
                JsonVariacion = jsonVariacion
            });
        }
    }
}