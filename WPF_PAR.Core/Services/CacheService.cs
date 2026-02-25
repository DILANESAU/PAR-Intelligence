using Dapper;

using System.Data.SqlClient;
using System.Text.Json;

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

        public async Task GuardarFamiliasAsync(int idSucursal, List<FamiliaResumenModel> datos)
        {
            string json = JsonSerializer.Serialize(datos);
            string sql = @"
                MERGE Cache_Familias AS target
                USING (SELECT @IdSucursal AS IdSucursal, @Anio AS Anio, @Mes AS Mes) AS source
                ON (target.IdSucursal = source.IdSucursal AND target.Anio = source.Anio AND target.Mes = source.Mes)
                WHEN MATCHED THEN
                    UPDATE SET JsonResumen = @Json, FechaActualizacion = GETDATE()
                WHEN NOT MATCHED THEN
                    INSERT (IdSucursal, Anio, Mes, JsonResumen, FechaActualizacion)
                    VALUES (@IdSucursal, @Anio, @Mes, @Json, GETDATE());";

            await _sqlHelper.ExecuteAsync(sql, new
            {
                IdSucursal = idSucursal,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month,
                Json = json
            });
        }

        public async Task GuardarClientesAsync(int idSucursal, object datosClientes)
        {
            string json = JsonSerializer.Serialize(datosClientes);

            string sql = @"
                MERGE Cache_Clientes AS target
                USING (SELECT @IdSucursal AS IdSucursal, @Anio AS Anio, @Mes AS Mes) AS source
                ON (target.IdSucursal = source.IdSucursal AND target.Anio = source.Anio AND target.Mes = source.Mes)
                WHEN MATCHED THEN
                    UPDATE SET JsonData = @Json, FechaActualizacion = GETDATE()
                WHEN NOT MATCHED THEN
                    INSERT (IdSucursal, Anio, Mes, JsonData, FechaActualizacion)
                    VALUES (@IdSucursal, @Anio, @Mes, @Json, GETDATE());";

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

            var jsonResult = ( await _sqlHelper.QueryAsync<string>(sql, new
            {
                IdSucursal = idSucursal,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month
            }) ).FirstOrDefault();

            if ( string.IsNullOrEmpty(jsonResult) )
                return new List<FamiliaResumenModel>(); 

            return JsonSerializer.Deserialize<List<FamiliaResumenModel>>(jsonResult);
        }
    }
}