using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class ClienteResumenModel
    {
        public string IdCliente { get; set; }
        public string Nombre { get; set; }
        public string Clasificacion { get; set; } // Sugerencia: "A", "B", "C"

        // --- ANUAL ---
        public decimal VentaAnualActual { get; set; }
        public double LitrosAnualActual { get; set; }
        public decimal VentaAnualAnterior { get; set; }

        // Crecimiento Anual ($)
        public decimal DiferenciaAnual => VentaAnualActual - VentaAnualAnterior;
        public double PorcentajeCrecimiento => VentaAnualAnterior == 0 ? 1 : ( double ) ( ( VentaAnualActual - VentaAnualAnterior ) / VentaAnualAnterior );

        // --- TRIMESTRES (Q1, Q2, Q3, Q4) ---
        // (Repetimos patrón para Q1...Q4. Aquí pongo ejemplo de Q1)
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

        // --- SEMESTRES ---
        public decimal VentaS1Actual { get; set; } // Ene-Jun
        public double LitrosS1Actual { get; set; }
        public decimal VentaS1Anterior { get; set; }

        public decimal VentaS2Actual { get; set; } // Jul-Dic
        public double LitrosS2Actual { get; set; }
        public decimal VentaS2Anterior { get; set; }

        // --- MENSUAL ---
        public List<decimal> HistoriaMensualActual { get; set; } = new List<decimal>();
    }
}
