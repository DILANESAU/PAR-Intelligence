using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class PeriodoBloque
    {
        public string Etiqueta { get; set; } // "Q1", "Sem 1"
        public decimal Valor { get; set; }   // $50,000
        public double Litros { get; set; }   // 10,000 L
        public bool EsFuturo { get; set; }   // Para pintar de gris lo que aun no pasa
    }
    public class SubLineaPerformanceModel
    {
        public string Nombre { get; set; }
        public decimal VentaTotal { get; set; }
        public double LitrosTotales { get; set; }
        public List<PeriodoBloque> Bloques { get; set; }
    }
}
