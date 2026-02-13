using System;
using System.Collections.Generic;
using System.Text;

using WPF_PAR.MVVM.Models;

namespace WPF_PAR.Core
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
