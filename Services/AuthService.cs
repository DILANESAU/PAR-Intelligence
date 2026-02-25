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
            // 1. Buscamos al usuario
            // Usamos AS Username para que Dapper lo mapee automático a la propiedad Username
            string query = @"
                SELECT 
                    IdUsuario,
                    [user] AS Username, 
                    NombreCompleto, 
                    Correo,
                    Rol
                FROM Usuarios 
                WHERE [user] = @User AND Clave = @Pass";

            var parametros = new { User = usuarioInput, Pass = passwordInput };

            // QueryAsync<T> hace el mapeo por nosotros
            var usuarios = await _authSqlHelper.QueryAsync<UsuarioModel>(query, parametros);
            var usuarioEncontrado = usuarios.FirstOrDefault();

            if ( usuarioEncontrado == null ) return null;

            // 2. Lógica de Permisos
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

                // Traemos la lista de IDs directamente como una lista de int
                var listaIds = await _authSqlHelper.QueryAsync<int>(queryPermisos, paramsPermisos);

                usuarioEncontrado.SucursalesPermitidas = listaIds.Any()
                    ? listaIds
                    : new List<int>();
            }

            return usuarioEncontrado;
        }
    }
}