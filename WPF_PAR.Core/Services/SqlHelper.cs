using Dapper;

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

        // 1. Para traer listas de cosas (Ej. Lista de sucursales)
        public async Task<List<T>> QueryAsync<T>(string sql, object? param = null)
        {
            using ( var connection = new SqlConnection(_connectionString) )
            {
                await connection.OpenAsync();
                var result = await connection.QueryAsync<T>(sql, param, commandTimeout: 300);
                return result.AsList();
            }
        }

        // 2. ¡NUEVO! Para traer un solo valor o fila (Ej. Nuestro texto JSON de la celda) 📦
        public async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
        {
            using ( var connection = new SqlConnection(_connectionString) )
            {
                await connection.OpenAsync();
                // Dapper se encarga de traer solo la primera coincidencia (o el valor default si no hay)
                return await connection.QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: 300);
            }
        }

        // 3. Para insertar, actualizar o borrar
        public async Task<int> ExecuteAsync(string sql, object? param = null)
        {
            using ( var connection = new SqlConnection(_connectionString) )
            {
                await connection.OpenAsync();
                return await connection.ExecuteAsync(sql, param, commandTimeout: 300);
            }
        }

        // 4. Para validar que la IP/Contraseña sigan vivas
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