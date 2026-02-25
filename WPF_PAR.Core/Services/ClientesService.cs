using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services
{
    public class ClientesService
    {
        private readonly SqlHelper _sqlHelper;

        public ClientesService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
        }

        public async Task<List<ClienteAnalisisModel>> ObtenerDatosBase(int anioActual, int sucursalId)
        {
            int anioAnterior = anioActual - 1;
            string filtroSucursal = sucursalId > 0 ? "AND v.Sucursal = @Sucursal" : "";

            string query = $@"
            SELECT 
                v.Cliente,
                ISNULL(MAX(c.Nombre), 'Cliente General') AS Nombre,
                v.Ejercicio,
                MONTH(v.FechaEmision) as Mes,
                SUM(vd.Cantidad * vd.Precio) as TotalDinero,
                SUM(vd.Cantidad) as TotalLitros
            FROM Venta v
            JOIN VentaD vd ON v.ID = vd.ID 
            LEFT JOIN Cte c ON v.Cliente = c.Cliente
            WHERE 
                v.Estatus = 'CONCLUIDO'
                {filtroSucursal}
                AND v.Ejercicio IN (@AnioActual, @AnioAnterior)
                AND (v.Mov LIKE 'Factura%' OR v.Mov LIKE 'Remisi%n%' OR v.Mov LIKE 'Nota%')
            GROUP BY v.Cliente, v.Ejercicio, MONTH(v.FechaEmision)";

            var parametros = new { AnioActual = anioActual, AnioAnterior = anioAnterior, Sucursal = sucursalId };

            var listaCruda = await _sqlHelper.QueryAsync<dynamic>(query, parametros);

            int mesesAComparar = 12;
            if ( anioActual == DateTime.Now.Year ) mesesAComparar = DateTime.Now.Month;

            var listaTipada = listaCruda.Select(r => new
            {
                Cliente = ( string ) r.Cliente,
                Nombre = ( string ) r.Nombre,
                Ejercicio = ( int ) r.Ejercicio,
                Mes = ( int ) r.Mes,
                TotalDinero = ( decimal ) r.TotalDinero,
                TotalLitros = ( decimal ) r.TotalLitros
            }).ToList();

            var clientesAgrupados = listaTipada
                .GroupBy(x => new { x.Cliente, x.Nombre })
                .Select(g => new ClienteAnalisisModel
                {
                    Cliente = g.Key.Cliente,
                    Nombre = g.Key.Nombre,
                    MesesParaCalculoTendencia = mesesAComparar,

                    VentasMensualesActual = Enumerable.Range(1, 12)
                        .Select(m => g.Where(x => x.Ejercicio == anioActual && x.Mes == m).Sum(v => v.TotalDinero))
                        .ToArray(),

                    VentasMensualesAnterior = Enumerable.Range(1, 12)
                        .Select(m => g.Where(x => x.Ejercicio == anioAnterior && x.Mes == m).Sum(v => v.TotalDinero))
                        .ToArray(),

                    LitrosMensualesActual = Enumerable.Range(1, 12)
                        .Select(m => g.Where(x => x.Ejercicio == anioActual && x.Mes == m).Sum(v => v.TotalLitros))
                        .ToArray()
                })
                .Where(x => x.VentasMensualesActual.Sum() > 0 || x.VentasMensualesAnterior.Sum() > 0)
                .OrderByDescending(x => x.VentasMensualesActual.Sum())
                .ToList();

            return clientesAgrupados;
        }

        public async Task<KpiClienteModel> ObtenerKpisCliente(string cliente, int anio, int sucursalId)
        {
            string filtroSucursal = sucursalId > 0 ? "AND Sucursal = @Sucursal" : "";

            string query = $@"
            SELECT 
                COUNT(DISTINCT MovID) as FrecuenciaCompra, -- Alias exacto del modelo
                ISNULL(SUM(PrecioTotal), 0) as TicketPromedio, -- Truco: Traemos el Total en este alias temporal
                MAX(FechaEmision) as UltimaCompra -- Alias exacto del modelo
            FROM Venta
            WHERE 
                Estatus = 'CONCLUIDO'
                AND Cliente = @Cliente
                AND Ejercicio = @Anio
                {filtroSucursal}
                AND (Mov LIKE 'Factura%' OR Mov LIKE 'Remisi%n%' OR Mov LIKE 'Nota%')";

            var parametros = new { Cliente = cliente, Anio = anio, Sucursal = sucursalId };

            var resultados = await _sqlHelper.QueryAsync<KpiClienteModel>(query, parametros);
            var kpi = resultados.FirstOrDefault() ?? new KpiClienteModel();

            if ( kpi.FrecuenciaCompra > 0 )
            {
                kpi.TicketPromedio = kpi.TicketPromedio / kpi.FrecuenciaCompra;
            }

            return kpi;
        }

        public async Task<List<ProductoAnalisisModel>> ObtenerVariacionProductos(string cliente, int anioActual, int sucursalId)
        {
            int anioAnterior = anioActual - 1;
            string filtroSucursal = sucursalId > 0 ? "AND v.Sucursal = @Sucursal" : "";
            int mesLimite = 12;
            if ( anioActual == DateTime.Now.Year ) mesLimite = DateTime.Now.Month;

            string query = $@"
            WITH CalculoBase AS (
                SELECT 
                    vd.Articulo,
                    ISNULL(MAX(a.Descripcion1), MAX(vd.Articulo)) as Descripcion,
                    
                    ISNULL(SUM(CASE 
                        WHEN v.Ejercicio = @AnioActual AND MONTH(v.FechaEmision) <= @MesLimite 
                        THEN (vd.Cantidad * vd.Precio) ELSE 0 END), 0) AS VentaActual,

                    ISNULL(SUM(CASE 
                        WHEN v.Ejercicio = @AnioAnterior AND MONTH(v.FechaEmision) <= @MesLimite 
                        THEN (vd.Cantidad * vd.Precio) ELSE 0 END), 0) AS VentaAnterior

                FROM VentaD vd
                JOIN Venta v ON vd.ID = v.ID
                LEFT JOIN Art a ON vd.Articulo = a.Articulo
                WHERE 
                    v.Cliente = @Cliente
                    AND v.Estatus = 'CONCLUIDO'
                    {filtroSucursal} 
                    AND v.Ejercicio IN (@AnioActual, @AnioAnterior)
                GROUP BY vd.Articulo
            )
            SELECT TOP 10 *
            FROM CalculoBase
            WHERE (VentaActual - VentaAnterior) <> 0 
            ORDER BY ABS(VentaActual - VentaAnterior) DESC";

            var parametros = new
            {
                Cliente = cliente,
                AnioActual = anioActual,
                AnioAnterior = anioAnterior,
                Sucursal = sucursalId,
                MesLimite = mesLimite
            };

            return await _sqlHelper.QueryAsync<ProductoAnalisisModel>(query, parametros);
        }
    }
}