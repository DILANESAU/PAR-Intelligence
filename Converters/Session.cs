using System;
using System.Collections.Generic;
using System.Text;

using WPF_PAR.Core.Models;

namespace WPF_PAR.Converters
{
    public static class Session
    {
        public static UsuarioModel UsuarioActual { get; set; }
        public static void Logout()
        {
            UsuarioActual = null;
        }
    }
}
