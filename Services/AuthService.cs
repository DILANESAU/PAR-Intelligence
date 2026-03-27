using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;

namespace WPF_PAR.Services
{
    public class AuthService
    {
        private readonly SqlHelper _authSqlHelper;

        // Constructor que recibe la cadena de conexión
        public AuthService(string connectionString)
        {
            _authSqlHelper = new SqlHelper(connectionString);
        }

        public async Task<UsuarioModel> ValidarLoginAsync(string usuarioInput, string passwordInput)
        {
            // 1. Buscamos al usuario HACIENDO JOIN CON ROLES
            // Traemos el Nombre del Rol para poder validarlo después
            string query = @"
        SELECT 
            u.IdUsuario,
            u.[user] AS Username, 
            u.NombreCompleto, 
            u.Correo,
            u.IdRol,
            r.Nombre AS Rol -- Traemos el texto 'Admin', 'Director', etc.
        FROM Usuarios u
        INNER JOIN Roles r ON u.IdRol = r.IdRol
        WHERE u.[user] = @User AND u.Clave = @Pass AND u.Activo = 1";

            var parametros = new { User = usuarioInput, Pass = passwordInput };

            var usuarios = await _authSqlHelper.QueryAsync<UsuarioModel>(query, parametros);
            var usuarioEncontrado = usuarios.FirstOrDefault();

            if ( usuarioEncontrado == null ) return null;

            // 2. Lógica de Permisos (Ahora 'Rol' ya tiene el texto gracias al JOIN)
            if ( usuarioEncontrado.Rol.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                usuarioEncontrado.Rol.Equals("Director", StringComparison.OrdinalIgnoreCase) )
            {
                usuarioEncontrado.SucursalesPermitidas = null; // Acceso total
            }
            else
            {
                string queryPermisos = @"
            SELECT IdSucursal 
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