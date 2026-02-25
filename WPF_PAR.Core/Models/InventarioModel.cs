using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core.Models
{
    public class InventarioModel
    {
        public string Clave { get; set; }
        public string Producto { get; set; }
        public string Unidad { get; set; }

        public double Existencia { get; set; }
        public double MinimoUnidades { get; set; }
        public double MaximoUnidades { get; set; }

        public double FactorLitros { get; set; }

        public double TotalLitros => Existencia * FactorLitros;
        public double MinimoLitros => MinimoUnidades * FactorLitros;
        public double MaximoLitros => MaximoUnidades * FactorLitros;

        public string Situacion
        {
            get
            {
                if ( FactorLitros <= 0 || MaximoUnidades == 0 ) return "NORMAL";

                if ( TotalLitros < MinimoLitros ) return "BAJO";
                if ( TotalLitros > MaximoLitros ) return "EXCESO";
                return "OPTIMO";
            }
        }

        public string ColorEstado
        {
            get
            {
                switch ( Situacion )
                {
                    case "BAJO": return "#F44336";
                    case "EXCESO": return "#FFC107";
                    case "OPTIMO": return "#4CAF50";
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
