using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core.Models
{
    public class UsuarioModel
    {
        public int IdUsuario { get; set; }
        public string Username { get; set; }
        public string NombreCompleto { get; set; }
        public string PasswordHash { get; set; }
        public string Rol { get; set; }
        public List<int> SucursalesPermitidas { get; set; }
    }
}
