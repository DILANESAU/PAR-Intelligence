using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core.Models
{
    public class ClienteResumenModel
    {
        public string IdCliente { get; set; }
        public string Nombre { get; set; }
        public string Clasificacion { get; set; }

        public decimal VentaAnualActual { get; set; }
        public double LitrosAnualActual { get; set; }
        public decimal VentaAnualAnterior { get; set; }

        public decimal DiferenciaAnual => VentaAnualActual - VentaAnualAnterior;
        public double PorcentajeCrecimiento => VentaAnualAnterior == 0 ? 1 : ( double ) ( ( VentaAnualActual - VentaAnualAnterior ) / VentaAnualAnterior );
        public decimal VentaQ1Actual { get; set; }
        public double LitrosQ1Actual { get; set; }
        public decimal VentaQ1Anterior { get; set; }
        public double PorcentajeQ1 => VentaQ1Anterior == 0 ? 0 : ( double ) ( ( VentaQ1Actual - VentaQ1Anterior ) / VentaQ1Anterior );

        public decimal VentaQ2Actual { get; set; }
        public double LitrosQ2Actual { get; set; }
        public decimal VentaQ2Anterior { get; set; }
        public double PorcentajeQ2 => VentaQ2Anterior == 0 ? 0 : ( double ) ( ( VentaQ2Actual - VentaQ2Anterior ) / VentaQ2Anterior );

        public decimal VentaQ3Actual { get; set; }
        public double LitrosQ3Actual { get; set; }
        public decimal VentaQ3Anterior { get; set; }

        public decimal VentaQ4Actual { get; set; }
        public double LitrosQ4Actual { get; set; }
        public decimal VentaQ4Anterior { get; set; }

        public decimal VentaS1Actual { get; set; }
        public double LitrosS1Actual { get; set; }
        public decimal VentaS1Anterior { get; set; }

        public decimal VentaS2Actual { get; set; }
        public double LitrosS2Actual { get; set; }
        public decimal VentaS2Anterior { get; set; }

        public List<decimal> HistoriaMensualActual { get; set; } = new List<decimal>();
    }
}
