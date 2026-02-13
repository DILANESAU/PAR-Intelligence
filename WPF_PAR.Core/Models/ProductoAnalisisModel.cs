using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class ProductoAnalisisModel
    {
        public string Articulo { get; set; }
        public string Descripcion { get; set; }
        public decimal VentaAnterior { get; set; } 
        public decimal VentaActual { get; set; } 
        public decimal Diferencia => VentaActual - VentaAnterior;
        public bool EsPerdida => Diferencia < 0;
    }
    public class KpiClienteModel
    {
        public decimal TicketPromedio { get; set; }
        public int FrecuenciaCompra { get; set; } 
        public DateTime UltimaCompra { get; set; }
    }
}
