using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core.Models
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
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal TotalVenta { get; set; }
        public decimal LitrosUnitarios { get; set; }
        public decimal LitrosTotales => Cantidad * LitrosUnitarios;
        public double LitrosTotal { get; set; }

        public string Agente { get; set; }
        public decimal TotalCosto { get; set; }

        // 2️⃣ MAGIA C#: Se calculan solas en milisegundos sin ir a la base de datos
        public decimal UtilidadBruta => TotalVenta - TotalCosto;
        public decimal MargenPorcentaje => TotalVenta > 0 ? ( UtilidadBruta / TotalVenta ) : 0;
    }
}