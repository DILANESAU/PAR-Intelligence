using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class VentaReporteModel
    {
        public DateTime FechaEmision { get; set; }
        public string Sucursal { get; set; }
        public string Mov { get; set; }
        public string MovID { get; set; }
        public string Cliente { get; set; }
        public string Articulo { get; set; }
        public string Descripcion { get; set; }
        public string Familia { get; set; }
        public string Linea { get; set; }
        public string Color { get; set; }
        public double Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal TotalVenta { get; set; }
        public double LitrosUnitarios { get; set; }
        public double LitrosTotales => ( double ) Cantidad * LitrosUnitarios;
        public double LitrosTotal { get; set; }
    }
}