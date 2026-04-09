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
            // 1. Buscamos al usuario por su Username (SIN CHECAR EL PASSWORD TODAVÍA)
            // Traemos el Hash de la base de datos para compararlo en memoria de forma segura.
            string query = @"
                SELECT 
                    u.Id AS IdUsuario,          -- Renombrado para que encaje con tu UsuarioModel
                    u.Username AS Username, 
                    u.PasswordHash,             -- NECESITAMOS TRAER EL HASH
                    u.NombreCompleto, 
                    u.RolId AS IdRol,
                    r.Nombre AS Rol
                FROM Usuarios u
                INNER JOIN Roles r ON u.RolId = r.Id
                WHERE u.Username = @User AND u.Activo = 1";

            var parametros = new { User = usuarioInput };
            var usuarios = await _authSqlHelper.QueryAsync<UsuarioModel>(query, parametros);
            var usuarioEncontrado = usuarios.FirstOrDefault();
            

            // Si no existe el usuario
            if (usuarioEncontrado == null) return null;

            // =========================================================================
            // 2. VALIDAMOS LA CONTRASEÑA (HASHING)
            // =========================================================================
            // Usamos la clase PasswordHasher que creamos arriba. 
            // Si prefieres hacerlo en texto plano por ahora (NO RECOMENDADO), cambia esto por: 
            // if(usuarioEncontrado.PasswordHash != passwordInput) return null;

            bool esValido = PasswordHasher.VerifyPassword(passwordInput, usuarioEncontrado.PasswordHash);
            if (!esValido) return null;

            // =========================================================================
            // 3. LÓGICA DE PERMISOS DE SUCURSAL (La tuya estaba excelente)
            // =========================================================================
            if (usuarioEncontrado.Rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
                 usuarioEncontrado.Rol.Equals("Director", StringComparison.OrdinalIgnoreCase))
            {
                usuarioEncontrado.SucursalesPermitidas = null; // Acceso total
            }
            else
            {
                // NOTA: Para que esto funcione, necesitas asegurarte de tener la tabla 'UsuarioSucursales' 
                // en tu base de datos ParSystem con los campos IdUsuario e IdSucursal.
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

        public async Task<bool> RegistrarUsuarioAsync(string username, string passwordPlana, string nombreCompleto, int rolId)
        {
            try
            {
                // 1. Encriptamos la contraseña plana ("admin123" -> "AQAAAAEAACcQ...")
                string hash = PasswordHasher.HashPassword(passwordPlana);

                // 2. Insertamos en la base de datos
                string query = @"
            INSERT INTO Usuarios (Username, PasswordHash, NombreCompleto, RolId, Activo, FechaCreacion)
            VALUES (@User, @Hash, @Nombre, @RolId, 1, GETDATE())";

                var parametros = new { User = username, Hash = hash, Nombre = nombreCompleto, RolId = rolId };

                int filasAfectadas = await _authSqlHelper.ExecuteAsync(query, parametros);
                return filasAfectadas > 0;
            }
            catch (Exception ex)
            {
                // Esto fallará (y está bien) si intentas registrar un Username que ya existe
                System.Diagnostics.Debug.WriteLine("Error al registrar usuario: " + ex.Message);
                return false;
            }
        }
    }
}