using WPF_PAR.Core.Models;

using System.Collections.Generic;
using System.Threading.Tasks;

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
                    SUM(Cantidad) as LitrosTotal  -- <--- Renombrado para coincidir con tu Modelo
                FROM Venta 
                WHERE Sucursal = @Sucursal 
                  AND FechaEmision BETWEEN @Inicio AND @Fin
                  AND Estatus = 'CONCLUIDO'
                GROUP BY Articulo";

            var parametros = new
            {
                Sucursal = idSucursal,
                Inicio = inicio,
                Fin = fin
            };

            return await _sqlHelper.QueryAsync<VentaReporteModel>(query, parametros);
        }
    }
}