using System;
using System.Collections.Generic;
using System.Text;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;

namespace WPF_PAR.Services
{
    public class UsuariosService
    {
        private readonly SqlHelper _sqlHelper;

        public UsuariosService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
        }
        public async Task<List<UsuarioModel>> ObtenerUsuariosAsync()
        {
            string query = @"
                SELECT 
                    u.Id AS IdUsuario, 
                    u.Username, 
                    u.NombreCompleto, 
                    u.Activo, 
                    r.Nombre AS Rol 
                FROM Usuarios u
                INNER JOIN Roles r ON u.RolId = r.Id
                ORDER BY u.NombreCompleto";

            var lista = await _sqlHelper.QueryAsync<UsuarioModel>(query, null);
            return lista.ToList();
        }
        public async Task<bool> CambiarEstadoAsync(string username, bool nuevoEstado)
        {
            int estadoSql = nuevoEstado ? 1 : 0;

            string query = "UPDATE Usuarios SET Activo = @Estado WHERE Username = @User";
            int filas = await _sqlHelper.ExecuteAsync(query, new { Estado = estadoSql, User = username });
            return filas > 0;
        }
        public async Task<bool> ResetearPasswordAsync(string username, string passwordTemporalPlana)
        {
            string hashTemporal = PasswordHasher.HashPassword(passwordTemporalPlana);
            string query = @"
                UPDATE Usuarios 
                SET PasswordHash = @Hash, RequiereCambioPwd = 1 
                WHERE Username = @User";

            int filas = await _sqlHelper.ExecuteAsync(query, new { Hash = hashTemporal, User = username });
            return filas > 0;
        }
    }
}
