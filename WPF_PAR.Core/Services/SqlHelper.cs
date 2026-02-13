

using Microsoft.Data.SqlClient;

namespace WPF_PAR.Core.Services
{
    public class SqlHelper
    {
        private readonly string _connectionString;
        public enum TipoConexion { Auth, Data }
        public SqlHelper(string connectionString)
        {
            _connectionString = connectionString;
        }
        public async Task<bool> ProbarConexionAsync()
        {
            try
            {
                using ( var conexion = new SqlConnection(_connectionString) )
                {
                    await conexion.OpenAsync();
                    return true; // ¡Éxito!
                }
            }
            catch ( Exception ex )
            {
                // Aquí podrías loguear el error si quisieras
                System.Diagnostics.Debug.WriteLine("Error conexión: " + ex.Message);
                return false;
            }
        }

        // ====================================================================
        // TUS MÉTODOS EXISTENTES (Sin cambios lógicos, solo siguen usando _connectionString)
        // ====================================================================

        // En SqlHelper.cs, dentro de la clase SqlHelper

        // MÉTODO NUEVO: Para ejecutar INSERT, UPDATE, DELETE o MERGE
        public async Task<int> ExecuteAsync(string query, Dictionary<string, object> parameters)
        {
            using ( var conexion = new SqlConnection(_connectionString) )
            {
                await conexion.OpenAsync();
                using ( var comando = new SqlCommand(query, conexion) )
                {
                    if ( parameters != null )
                    {
                        foreach ( var param in parameters )
                        {
                            // Manejo de nulos
                            comando.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }
                    try
                    {
                        return await comando.ExecuteNonQueryAsync(); // Retorna filas afectadas
                    }
                    catch ( Exception ex )
                    {
                        throw new Exception($"Error al ejecutar comando SQL: {ex.Message}", ex);
                    }
                }
            }
        }
        public async Task<List<T>> QueryAsync<T>(string query, Dictionary<string, object> parameters, Func<SqlDataReader, T> mapFunction)
        {
            var lista = new List<T>();

            using ( var conexion = new SqlConnection(_connectionString) )
            {
                await conexion.OpenAsync();

                using ( var comando = new SqlCommand(query, conexion) )
                {
                    if ( parameters != null )
                    {
                        foreach ( var param in parameters )
                        {
                            comando.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    try
                    {
                        using ( var lector = await comando.ExecuteReaderAsync() )
                        {
                            while ( await lector.ReadAsync() )
                            {
                                lista.Add(mapFunction(lector));
                            }
                        }
                    }
                    catch ( Exception ex )
                    {
                        throw new Exception($"Error al ejecutar SQL: {ex.Message}", ex);
                    }
                }
            }
            return lista;
        }
    }
}