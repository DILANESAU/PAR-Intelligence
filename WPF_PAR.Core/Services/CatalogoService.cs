using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services.Interfaces;

namespace WPF_PAR.Core.Services
{
    public class CatalogoService
    {
        private Dictionary<string, ProductoInfo> _catalogo;
        private readonly SqlHelper _sql;
        private readonly IBusinessLogicService _businessLogic;

        public CatalogoService(IBusinessLogicService businessLogic)
        {
            _businessLogic = businessLogic;
            _catalogo = new Dictionary<string, ProductoInfo>(StringComparer.OrdinalIgnoreCase);
            // Nos conectamos a ParSystem (donde vive tu tabla Catalogo_Productos)
            _sql = new SqlHelper(businessLogic.GetParSystemConnectionString());
        }

        // ========================================================
        // DESCARGA TODO DE SQL AL DICCIONARIO
        // ========================================================
        public async Task CargarCatalogoSqlAsync()
        {
            try
            {
                // Hacemos que los alias de SQL coincidan EXACTO con tu ProductoInfo
                string query = @"
                    SELECT 
                        CodigoArticulo, 
                        Descripcion, 
                        Categoria,
                        Grupo,
                        Familia, 
                        ISNULL([A.Linea], 'Sin Linea') AS Linea, 
                        ISNULL([Color-Tipo], 'Sin Color') AS ColorTipo, 
                        CAST(ISNULL(Litros, 0) AS DECIMAL(18,4)) AS Litros 
                    FROM Catalogo_Productos";

                var listaSql = await _sql.QueryAsync<ProductoInfo>(query);

                _catalogo.Clear(); // Limpiamos por si el Worker se reinicia

                foreach (var item in listaSql)
                {
                    // Limpiamos la familia usando tu regla de negocio (quitar acentos, etc.)
                    item.Familia = _businessLogic.NormalizarFamilia(item.Familia ?? "Otros");

                    // Limpieza de nulos por seguridad
                    item.CodigoArticulo = item.CodigoArticulo?.Trim() ?? "";
                    item.Descripcion = item.Descripcion?.Trim() ?? "Sin Descripción";

                    // Lo agregamos al diccionario usando CodigoArticulo como llave
                    if (!string.IsNullOrEmpty(item.CodigoArticulo) && !_catalogo.ContainsKey(item.CodigoArticulo))
                    {
                        _catalogo.Add(item.CodigoArticulo, item);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERROR CRÍTICO SQL CATÁLOGO: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Error cargando catálogo desde SQL: " + ex.Message);
            }
        }

        // ========================================================
        // BÚSQUEDA RÁPIDA (Usado por el Worker en cada vuelta)
        // ========================================================
        public ProductoInfo ObtenerInfo(string codigoProducto)
        {
            if (!string.IsNullOrWhiteSpace(codigoProducto) && _catalogo.ContainsKey(codigoProducto))
            {
                return _catalogo[codigoProducto];
            }

            // Producto por defecto "comodín" si se vende algo que no está en la tabla
            return new ProductoInfo
            {
                CodigoArticulo = codigoProducto ?? "Desconocido",
                Descripcion = "Producto (Sin Catálogo)",
                Categoria = "Sin Categoria",
                Grupo = "Sin Grupo",
                Familia = "Otros", // Puedes mandar esto a "Accesorios" si lo prefieres
                Linea = "Sin Linea",
                ColorTipo = "Sin Color",
                Litros = 0m // La "m" es para decirle a C# que es un Decimal
            };
        }
    }
}