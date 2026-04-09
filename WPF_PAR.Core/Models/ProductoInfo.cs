using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core.Models
{
    public class ProductoInfo
    {
        public string CodigoArticulo { get; set; }
        public string Descripcion { get; set; }
        public string Categoria { get; set; }
        public string Grupo { get; set; }
        public string Familia { get; set; }
        public string Linea { get; set; } // Mapearemos A.Linea aquí
        public string ColorTipo { get; set; } // Mapearemos Color-Tipo aquí
        public decimal Litros { get; set; }
    }
}
