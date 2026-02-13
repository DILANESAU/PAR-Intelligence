using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class InventarioModel
    {
        public string Clave { get; set; }
        public string Producto { get; set; }
        public string Unidad { get; set; }

        // Datos Numéricos Base (Unidades)
        public double Existencia { get; set; }
        public double MinimoUnidades { get; set; }
        public double MaximoUnidades { get; set; }

        // Conversión a Litros
        public double FactorLitros { get; set; }

        // Propiedades Calculadas en Litros
        public double TotalLitros => Existencia * FactorLitros;
        public double MinimoLitros => MinimoUnidades * FactorLitros;
        public double MaximoLitros => MaximoUnidades * FactorLitros;

        // LÓGICA DE SEMÁFORO (KPI)
        // Retorna: "BAJO", "OK", "EXCESO"
        public string Situacion
        {
            get
            {
                // Si no es pintura (no tiene litros) o no tiene configurados máximos, ignoramos
                if ( FactorLitros <= 0 || MaximoUnidades == 0 ) return "NORMAL";

                if ( TotalLitros < MinimoLitros ) return "BAJO";
                if ( TotalLitros > MaximoLitros ) return "EXCESO";
                return "OPTIMO";
            }
        }

        // Propiedades visuales para la tabla
        public string ColorEstado
        {
            get
            {
                switch ( Situacion )
                {
                    case "BAJO": return "#F44336";   // Rojo (Poco stock)
                    case "EXCESO": return "#FFC107"; // Ámbar (Mucho stock)
                    case "OPTIMO": return "#4CAF50"; // Verde (Bien)
                    default: return "Transparent";
                }
            }
        }

        public string IconoEstado
        {
            get
            {
                switch ( Situacion )
                {
                    case "BAJO": return "ArrowDownBold";
                    case "EXCESO": return "ArrowUpBold";
                    case "OPTIMO": return "CheckBold";
                    default: return "";
                }
            }
        }
    }
}
