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
        public async Task<List<T>> QueryAsync<T>(string sql, object? param = null)
        {
            using ( var connection = new SqlConnection(_connectionString) )
            {
                await connection.OpenAsync();

                var result = await connection.QueryAsync<T>(sql, param);
                return result.AsList();
            }
        }
        public async Task<int> ExecuteAsync(string sql, object? param = null)
        {
            using ( var connection = new SqlConnection(_connectionString) )
            {
                await connection.OpenAsync();
                return await connection.ExecuteAsync(sql, param);
            }
        }

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