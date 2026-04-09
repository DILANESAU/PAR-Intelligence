using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core.Models
{
    public class PeriodoBloque
    {
        public string Etiqueta { get; set; }
        public decimal Valor { get; set; }
        public decimal Litros { get; set; }
        public bool EsFuturo { get; set; }
    }
    public class SubLineaPerformanceModel
    {
        public string Nombre { get; set; }
        public decimal VentaTotal { get; set; }
        public decimal LitrosTotales { get; set; }
        public List<PeriodoBloque> Bloques { get; set; }
    }
}
