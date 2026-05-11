using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services; // Para SqlHelper

namespace WPF_PAR.Services
{
    public class AuthService
    {
        private readonly SqlHelper _authSqlHelper;

        public AuthService(string connectionString)
        {
            _authSqlHelper = new SqlHelper(connectionString);
        }

        public async Task<UsuarioModel> ValidarLoginAsync(string usuarioInput, string passwordInput)
        {

            string query = @"
                SELECT 
                    u.Id AS IdUsuario,
                    u.Username, 
                    u.PasswordHash,
                    u.NombreCompleto, 
                    u.RolId,
                    r.Nombre AS Rol,
                    u.RequiereCambioPwd 
                FROM Usuarios u
                INNER JOIN Roles r ON u.RolId = r.Id
                WHERE u.Username = @User AND u.Activo = 1";

            var parametros = new { User = usuarioInput };
            var usuarios = await _authSqlHelper.QueryAsync<UsuarioModel>(query, parametros);
            var usuarioEncontrado = usuarios.FirstOrDefault();

            if (usuarioEncontrado == null) return null;

            bool esValido = PasswordHasher.VerifyPassword(passwordInput, usuarioEncontrado.PasswordHash);
            if (!esValido) return null;

            if (usuarioEncontrado.Rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
                usuarioEncontrado.Rol.Equals("Director", StringComparison.OrdinalIgnoreCase) ||
                usuarioEncontrado.Rol.Equals("Sistemas", StringComparison.OrdinalIgnoreCase))
            {
                usuarioEncontrado.SucursalesPermitidas = null; // Acceso total
            }
            else
            {
                string queryPermisos = @"
                    SELECT SucursalId 
                    FROM UsuarioSucursales
                    WHERE IdUsuario = @Id";

                var paramsPermisos = new { Id = usuarioEncontrado.IdUsuario };
                var listaIds = await _authSqlHelper.QueryAsync<int>(queryPermisos, paramsPermisos);

                usuarioEncontrado.SucursalesPermitidas = listaIds.ToList();
            }

            return usuarioEncontrado;
        }
    }
}