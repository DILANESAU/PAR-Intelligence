using System;
using System.Collections.Generic;
using System.Text;
using WPF_PAR.MVVM.Models;

namespace WPF_PAR.Services
{
    public class BusinessLogicService
    {
        private readonly List<FamiliaConfig> _configuraciones;

        public BusinessLogicService()
        {
            _configuraciones =
            [
                new() { NombreNormalizado = "Selladores", ColorHex = "#1F7045", PalabrasClave = ["SELLADOR"] },
                new() { NombreNormalizado = "Impermeabilizantes", ColorHex = "#5d9a9f", PalabrasClave = ["IMPER"] },
                new() { NombreNormalizado = "Tráfico", ColorHex = "#F9A825", PalabrasClave = ["TRAFICO", "TRÁFICO"] },
                new() { NombreNormalizado = "Industrial", ColorHex = "#664072", PalabrasClave = ["INDUSTRIAL"] },
                new() { NombreNormalizado = "Vinílica", ColorHex = "#D19755", PalabrasClave = ["VINIL", "VINÍLICA"] },
                new() { NombreNormalizado = "Esmaltes", ColorHex = "#4D1311", PalabrasClave = ["ESMALTE"] },
                new() { NombreNormalizado = "Maderas", ColorHex = "#8D6E63", PalabrasClave = ["MADERAS"] },
                new() { NombreNormalizado = "Solventes", ColorHex = "#005c4b", PalabrasClave = ["SOLVENTES"] },
                new() { NombreNormalizado = "Ferretería", ColorHex = "#4c85f3", PalabrasClave = ["FER-"] },
                new() { NombreNormalizado = "Accesorios", ColorHex = "#ddaea6", PalabrasClave = ["ACCESORIOS"] },
                new() { NombreNormalizado = "Activos Fijos", ColorHex = "#607D8B", PalabrasClave = ["AF-"] }
            ];
        }

        public string NormalizarFamilia(string textoRaw)
        {
            if ( string.IsNullOrEmpty(textoRaw) ) return "Otros";

            string mayus = textoRaw.ToUpper().Trim();

            var config = _configuraciones.FirstOrDefault(c => c.PalabrasClave.Any(k => mayus.Contains(k)));

            return config != null ? config.NombreNormalizado : "Otros";
        }

        public string ObtenerColorFamilia(string nombreFamilia)
        {
            var config = _configuraciones.FirstOrDefault(c => c.NombreNormalizado == nombreFamilia);

            return config != null ? config.ColorHex : "#616161";
        }
    }
}
