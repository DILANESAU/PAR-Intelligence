using WPF_PAR.Core.Services;
using WPF_PAR.Core.Models; // Asegúrate de tener un modelo para esto

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

namespace WPF_PAR.Core.Services
{
    public class IntelisisDataService
    {
        private readonly SqlHelper _sqlHelper;

        public IntelisisDataService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
        }

        public async Task<List<VentaReporteModel>> ObtenerVentasMesAsync(int idSucursal, DateTime inicio, DateTime fin)
        {
            string query = @"
                SELECT 
                    Articulo, 
                    SUM(VentaTotal) as TotalVenta, 
                    SUM(Cantidad) as Cantidad 
                FROM Venta 
                WHERE Sucursal = @Sucursal 
                  AND FechaEmision BETWEEN @Inicio AND @Fin
                  AND Estatus = 'CONCLUIDO'
                GROUP BY Articulo";

            var parametros = new Dictionary<string, object>
            {
                { "@Sucursal", idSucursal },
                { "@Inicio", inicio },
                { "@Fin", fin }
            };

            // Mapeamos el resultado a tu modelo
            return await _sqlHelper.QueryAsync(query, parametros, reader => new VentaReporteModel
            {
                Articulo = reader["Articulo"].ToString(),
                TotalVenta = Convert.ToDecimal(reader["TotalVenta"]),
                LitrosTotal = Convert.ToDouble(reader["Cantidad"])
            });
        }
    }
}