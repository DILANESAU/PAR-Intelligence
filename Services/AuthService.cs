using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WPF_PAR.MVVM.Models;

using static WPF_PAR.Services.SqlHelper;

namespace WPF_PAR.Services
{
    public class AuthService
    {
        private readonly SqlHelper _authSqlHelper;

        public AuthService()
        {
            // CAMBIO CRUCIAL:
            // Antes: new SqlHelper("AuthConnection");
            // Ahora: Le decimos explícitamente "Conéctate al servidor de AUTH"
            _authSqlHelper = new SqlHelper(TipoConexion.Auth);
        }

        public async Task<UsuarioModel> ValidarLoginAsync(string usuarioInput, string passwordInput)
        {
            UsuarioModel usuarioEncontrado = null;

            // ... (El resto de tu lógica SQL se queda IGUAL, está perfecta) ...

            string query = @"
                SELECT 
                    IdUsuario,
                    [user],
                    NombreCompleto, 
                    Correo,
                    Rol
                FROM Usuarios 
                WHERE [user] = @User AND Clave = @Pass";

            var parametros = new Dictionary<string, object>
            {
                { "@User", usuarioInput },
                { "@Pass", passwordInput }
            };

            var listaUsuarios = await _authSqlHelper.QueryAsync(query, parametros, lector =>
            {
                return new UsuarioModel
                {
                    IdUsuario = Convert.ToInt32(lector["IdUsuario"]),
                    Username = lector["user"].ToString(),
                    NombreCompleto = lector["NombreCompleto"].ToString(),
                    Rol = lector["Rol"].ToString(),
                    Password = "",
                    SucursalesPermitidas = null
                };
            });

            usuarioEncontrado = listaUsuarios.FirstOrDefault();

            if ( usuarioEncontrado == null ) return null;

            // Lógica de Permisos (Se queda igual)
            if ( usuarioEncontrado.Rol.Equals("Admin", StringComparison.OrdinalIgnoreCase) )
            {
                usuarioEncontrado.SucursalesPermitidas = null; // null significa "Todas"
            }
            else
            {
                string queryPermisos = @"
                    SELECT IdSucursal 
                    FROM UsuarioSucursales
                    WHERE IdUsuario = @Id";

                var paramsPermisos = new Dictionary<string, object> { { "@Id", usuarioEncontrado.IdUsuario } };

                var listaIds = await _authSqlHelper.QueryAsync(queryPermisos, paramsPermisos, lector =>
                {
                    return Convert.ToInt32(lector["IdSucursal"]);
                });

                if ( listaIds.Count > 0 )
                {
                    usuarioEncontrado.SucursalesPermitidas = listaIds;
                }
                else
                {
                    usuarioEncontrado.SucursalesPermitidas = new List<int>();
                }
            }

            return usuarioEncontrado;
        }
    }
}