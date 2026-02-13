using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class FamiliaResumenModel
    {
        public string NombreFamilia { get; set; }
        public decimal VentaTotal { get; set; }
        public double LitrosTotales { get; set; }
        public string MejorCliente { get; set; }
        public string ColorFondo { get; set; }
        public string ProductoEstrella { get; set; }
        public string ColorTexto { get; set; }
        public double PorcentajeParticipacion { get; set; } // Para la barra de progreso y el texto %
        public double LitrosTotal { get; set; }
    }
}
