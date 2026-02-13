using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core
{
    public static class ConfiguracionLineas
    {
        public static readonly List<string> Arquitectonica = new List<string>
        {
            "Vinílica",
            "Esmaltes",
            "Impermeabilizantes",
            "Selladores",
        };

        public static readonly List<string> Especializada = new List<string>
        {
            "Industrial",
            "Tráfico",
            "Solventes",
            "Accesorios",
        };
        public static List<string> ObtenerTodas()
        {
            var todas = new List<string>();
            todas.AddRange(Especializada);
            todas.AddRange(Arquitectonica);
            return todas;
        }
    }
}
