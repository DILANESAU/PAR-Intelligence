using Dapper; // <--- ¡Asegúrate de tener este using!

using Microsoft.Data.SqlClient;

using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Linq;

namespace WPF_PAR.Core.Services
{
    public class SqlHelper
    {
        private readonly string _connectionString;

        public SqlHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ====================================================================
        // MÉTODOS CON DAPPER (Lo que necesita CacheService)
        // ====================================================================

        // Para SELECTs. Mapea automático a tu Modelo.
        // Acepta parámetros anónimos: new { Id = 1 }
        public async Task<List<T>> QueryAsync<T>(string sql, object param = null)
        {
            using ( var connection = new SqlConnection(_connectionString) )
            {
                // No necesitas OpenAsync explícito, Dapper lo maneja, 
                // pero ponerlo es buena práctica para asegurar la conexión antes.
                await connection.OpenAsync();

                var result = await connection.QueryAsync<T>(sql, param);
                return result.AsList();
            }
        }

        // Para INSERT, UPDATE, DELETE, MERGE.
        // Retorna número de filas afectadas.
        public async Task<int> ExecuteAsync(string sql, object param = null)
        {
            using ( var connection = new SqlConnection(_connectionString) )
            {
                await connection.OpenAsync();
                return await connection.ExecuteAsync(sql, param);
            }
        }

        // Método de utilidad para validar conexión (Login/Settings)
        public async Task<bool> ProbarConexionAsync()
        {
            try
            {
                using ( var connection = new SqlConnection(_connectionString) )
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}