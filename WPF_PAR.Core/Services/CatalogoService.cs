using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using WPF_PAR.MVVM.Models;

namespace WPF_PAR.Services
{
    public class CatalogoService
    {
        private Dictionary<string, ProductoInfo> _catalogo;
        private readonly BusinessLogicService _businessLogic;
        public CatalogoService(BusinessLogicService businessLogic)
        {
            _businessLogic = businessLogic;
            _catalogo = [];
            CargarDesdeCSV();
        }
        private void CargarDesdeCSV()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Productos.csv");
                if ( !File.Exists(path) ) return;

                var lineas = File.ReadAllLines(path).Skip(1);

                foreach ( var linea in lineas )
                {
                    var col = ParseCsvLine(linea);
                    if ( col.Count < 8 ) continue;

                    string clave = col[0].Trim();
                    string descripcion = col[1].Trim().Replace("\"", "");
                    string familiaRaw = col[4].Trim();
                    string litrosStr = col[7].Trim();
                    string lineaRaw = col.Count > 5 ? col[5].Trim() : "Sin Linea";
                    string colorRaw = col.Count > 6 ? col[6].Trim() : "Sin Color";

                    if ( !double.TryParse(litrosStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double litros) )
                        litros = 0;

                    if ( !_catalogo.ContainsKey(clave) )
                    {
                        string familiaNormalizada = _businessLogic.NormalizarFamilia(familiaRaw);

                        _catalogo.Add(clave, new ProductoInfo
                        {
                            Clave = clave,
                            Descripcion = descripcion,
                            FamiliaSimple = familiaNormalizada, 
                            Litros = litros,
                            Linea = lineaRaw,
                            Color = colorRaw,
                        });
                    }
                }
            }
            catch ( Exception ex )
            {
                System.Diagnostics.Debug.WriteLine("Error cargando CSV: " + ex.Message);
            }
        }
        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string currentField = "";

            for ( int i = 0; i < line.Length; i++ )
            {
                char c = line[i];

                if ( c == '"' ) inQuotes = !inQuotes;
                else if ( c == ',' && !inQuotes ) { result.Add(currentField); currentField = ""; }
                else currentField += c;
            }
            result.Add(currentField);
            return result;
        }
        // Services/CatalogoService.cs

        public ProductoInfo ObtenerInfo(string claveProducto)
        {
            if ( _catalogo.ContainsKey(claveProducto) ) return _catalogo[claveProducto];

            // CORRECCIÓN: Asignar valores por defecto a TODAS las propiedades de texto
            // para evitar que sean null y causen errores al usar .Trim() o .ToUpper()
            return new ProductoInfo
            {
                FamiliaSimple = "Accesorios",
                Litros = 0,
                Descripcion = "Producto (Sin Catálogo)",
                Linea = "Sin Linea",
                Color = "Sin Color",
                FamiliaCsv = "Otros"
            };
        }
    }
}
