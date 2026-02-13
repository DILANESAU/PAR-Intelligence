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

        // --- MÉTODOS PARA EL WORKER (ESCRIBIR) ---

        public async Task GuardarFamiliasAsync(int idSucursal, List<FamiliaResumenModel> datos)
        {
            string json = JsonSerializer.Serialize(datos);

            // Lógica: "Si existe el registro de este mes, actualízalo. Si no, créalo."
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

        // --- MÉTODOS PARA LA APP WPF (LEER) ---

        public async Task<List<FamiliaResumenModel>> ObtenerFamiliasAsync(int idSucursal)
        {
            string sql = @"
                SELECT JsonResumen 
                FROM Cache_Familias 
                WHERE IdSucursal = @IdSucursal 
                  AND Anio = @Anio 
                  AND Mes = @Mes";

            // Obtenemos el texto JSON crudo de la base de datos
            var jsonResult = ( await _sqlHelper.QueryAsync<string>(sql, new
            {
                IdSucursal = idSucursal,
                Anio = DateTime.Now.Year,
                Mes = DateTime.Now.Month
            }) ).FirstOrDefault();

            if ( string.IsNullOrEmpty(jsonResult) )
                return new List<FamiliaResumenModel>(); // Retorna vacío si aún no calcula el worker

            // Convertimos el Texto a Objetos C#
            return JsonSerializer.Deserialize<List<FamiliaResumenModel>>(jsonResult);
        }
    }
}