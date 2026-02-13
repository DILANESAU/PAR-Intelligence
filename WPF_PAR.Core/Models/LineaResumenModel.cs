using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class LineaResumenModel
    {
        public string NombreLinea { get; set; }
        public decimal VentaTotal { get; set; }
        public double LitrosTotales { get; set; }
        public string ProductoTop { get; set; }
        public ObservableCollection<ColorResumenModel> TopColores { get; set; }
    }
    public class ColorResumenModel
    {
        public string NombreColor { get; set; }
        public double Litros { get; set; }
        public decimal Venta { get; set; }
    }
}
