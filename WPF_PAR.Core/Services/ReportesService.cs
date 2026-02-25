using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services
{
    public class ReportesService
    {
        private readonly SqlHelper _sqlHelper;

        public ReportesService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
        }

        public async Task<List<VentaReporteModel>> ObtenerVentasBrutasRango(int sucursal, DateTime inicio, DateTime fin)
        {
            string query = @"
            SELECT
                v.FechaEmision,
                vd.Sucursal,
                ISNULL((SELECT TOP 1 Nombre FROM Cte WHERE Cliente = v.Cliente), 'Cliente General') AS Cliente, -- Antes NombreCliente
                v.MovID,
                v.Mov,
                vd.Articulo,

                -- 1. CANTIDAD (Volumen) -> Mapea a Propiedad 'Cantidad'
                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                        WHEN v.Mov LIKE '%Bonifica%' THEN 0
                        ELSE vd.Cantidad
                    END
                ), 0) AS Cantidad, 

                -- 2. DINERO (Saldo) -> Mapea a Propiedad 'TotalVenta'
                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' THEN ((vd.Cantidad * vd.Precio) * -1)
                        WHEN v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
                        ELSE (vd.Cantidad * vd.Precio)
                    END
                ), 0) AS TotalVenta,

                -- 3. DESCUENTOS -> Mapea a Propiedad 'Descuento'
                ISNULL(SUM(
                    CASE 
                       WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.DescuentoImporte * -1)
                       ELSE vd.DescuentoImporte
                    END
                ), 0) AS Descuento

            FROM VentaD vd
            JOIN Venta v ON vd.ID = v.ID
            WHERE
                v.Estatus = 'CONCLUIDO'
                AND vd.Sucursal = @Sucursal
                AND v.FechaEmision >= @Inicio 
                AND v.FechaEmision < DATEADD(day, 1, @Fin)
                AND v.Mov NOT LIKE '%Pedido%'
                AND v.Mov NOT LIKE '%Venta Perdida%'
                AND v.Mov NOT LIKE '%Cotiza%'
                AND v.Mov NOT LIKE '%Carta Porte%'
            GROUP BY
                v.FechaEmision, vd.Sucursal, v.Cliente, v.MovID, v.Mov, vd.Articulo";
            var parametros = new { Sucursal = sucursal, Inicio = inicio, Fin = fin };

            var datos = await _sqlHelper.QueryAsync<VentaReporteModel>(query, parametros);

            foreach ( var item in datos )
            {
                decimal precioUnitarioVisual = 0;
                if ( Math.Abs(item.Cantidad) > 0.001 )
                {
                    precioUnitarioVisual = Math.Abs(item.TotalVenta / ( decimal ) item.Cantidad);
                }
                else if ( item.Mov != null && item.Mov.Contains("Bonifica") )
                {
                    precioUnitarioVisual = item.TotalVenta;
                }
                item.PrecioUnitario = Math.Abs(precioUnitarioVisual);
                item.Articulo = item.Articulo?.Trim();
            }

            return datos;
        }

        public async Task<List<VentaReporteModel>> ObtenerHistoricoAnualPorArticulo(string ejercicio, string sucursal)
        {
            string filtroSucursal = "";

            string sqlWhereSucursal = ( !string.IsNullOrEmpty(sucursal) && sucursal != "0" )
                                      ? "AND vd.Sucursal = @Sucursal"
                                      : "";

            string query = $@"
            SELECT 
                v.Periodo, -- Ojo: Tu modelo usa FechaEmision, tendremos que convertirlo después
                vd.Articulo,
                ISNULL((SELECT TOP 1 Nombre FROM Cte WHERE Cliente = v.Cliente), 'Cliente General') AS Cliente,
                
                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                        WHEN v.Mov LIKE '%Bonifica%' THEN 0
                        ELSE vd.Cantidad
                    END
                ), 0) AS Cantidad,

                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' THEN ((vd.Cantidad * vd.Precio) * -1)
                        WHEN v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
                        ELSE (vd.Cantidad * vd.Precio)
                    END
                ), 0) AS TotalVenta

            FROM VentaD vd
            JOIN Venta v ON vd.ID = v.ID
            WHERE 
                v.Ejercicio = @Ejercicio 
                AND v.Estatus = 'CONCLUIDO'
                {sqlWhereSucursal}
                AND v.Mov NOT LIKE '%Pedido%'
                AND v.Mov NOT LIKE '%Venta Perdida%'
                AND v.Mov NOT LIKE '%Cotiza%'
                AND v.Mov NOT LIKE '%Carta Porte%'
            GROUP BY v.Periodo, vd.Articulo, v.Cliente";

            var parametros = new { Ejercicio = ejercicio, Sucursal = sucursal };

            var resultadosCrudos = await _sqlHelper.QueryAsync<dynamic>(query, parametros);

            var listaFinal = new List<VentaReporteModel>();
            foreach ( var r in resultadosCrudos )
            {
                int periodo = ( int ) r.Periodo;
                double cant = ( double ) r.Cantidad;
                decimal total = ( decimal ) r.TotalVenta;
                int anio = int.Parse(ejercicio);

                listaFinal.Add(new VentaReporteModel
                {
                    FechaEmision = new DateTime(anio, periodo, 1),
                    Articulo = r.Articulo.ToString().Trim(),
                    Cliente = r.Cliente.ToString(),
                    Cantidad = cant,
                    TotalVenta = total,
                    LitrosUnitarios = 1,
                    PrecioUnitario = Math.Abs(cant) > 0.001 ? Math.Abs(total / ( decimal ) cant) : 0,
                    Descuento = 0
                });
            }

            return listaFinal;
        }

        public async Task<List<VentaReporteModel>> ObtenerVentasRangoAsync(int sucursalId, DateTime inicio, DateTime fin)
        {
            string filtroSucursal = sucursalId > 0 ? "AND vd.Sucursal = @Sucursal" : "";

            string query = $@"
            SELECT 
                v.FechaEmision, 
                vd.Sucursal,
                ISNULL(c.Nombre, 'Cliente General') as Cliente,
                v.MovID,
                v.Mov,
                vd.Articulo,
                
                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' 
                        THEN ((vd.Cantidad * vd.Precio) * -1)
                        ELSE (vd.Cantidad * vd.Precio)
                    END
                ), 0) AS TotalVenta, -- Alias corregido para el modelo

                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' 
                        THEN (vd.Cantidad * -1)
                        WHEN v.Mov LIKE '%Bonifica%' 
                        THEN 0 
                        ELSE vd.Cantidad
                    END
                ), 0) AS LitrosTotal -- Alias corregido

            FROM VentaD vd
            JOIN Venta v ON vd.ID = v.ID
            LEFT JOIN Cte c ON v.Cliente = c.Cliente
            WHERE 
                v.Estatus = 'CONCLUIDO'
                {filtroSucursal}
                AND v.FechaEmision >= @Inicio 
                AND v.FechaEmision < DATEADD(day, 1, @Fin)
                AND v.Mov NOT LIKE '%Pedido%'
                AND v.Mov NOT LIKE '%Venta Perdida%'
                AND v.Mov NOT LIKE '%Cotiza%'
                AND v.Mov NOT LIKE '%Carta Porte%'
            GROUP BY v.FechaEmision, vd.Sucursal, c.Nombre, v.MovID, v.Mov, vd.Articulo";

            var parametros = new { Sucursal = sucursalId, Inicio = inicio, Fin = fin };

            return await _sqlHelper.QueryAsync<VentaReporteModel>(query, parametros);
        }

        public async Task<List<VentaReporteModel>> ObtenerVentaAnualAsync(int sucursalId, int anio)
        {
            string query = @"
             SELECT 
                v.Periodo AS Mov, -- Truco: Usamos la propiedad Mov para guardar el numero de mes temporalmente
                SUM(v.PrecioTotal) AS PrecioUnitario -- Truco: Usamos PrecioUnitario para guardar el total
            FROM Venta v
            WHERE 
                v.Estatus = 'CONCLUIDO'
                AND v.Sucursal = @Sucursal 
                AND v.Ejercicio = @Ejercicio 
                AND v.Mov Like 'Factura%'
            GROUP BY v.Periodo
            ORDER BY v.Periodo";

            var parametros = new { Sucursal = sucursalId, Ejercicio = anio };

            var datos = await _sqlHelper.QueryAsync<VentaReporteModel>(query, parametros);

            foreach ( var d in datos )
            {
                d.Cantidad = 1;
                d.Descuento = 0;
                d.FechaEmision = new DateTime(anio, 1, 1);
            }
            return datos;
        }

        public async Task<List<GraficoPuntoModel>> ObtenerTendenciaGrafica(int sucursalId, DateTime inicio, DateTime fin, bool agruparPorMes)
        {
            string filtroSucursal = sucursalId > 0 ? "AND vd.Sucursal = @Sucursal" : "";
            string agrupador = agruparPorMes ? "MONTH(v.FechaEmision)" : "DAY(v.FechaEmision)";

            string query = $@"
            SELECT 
                {agrupador} as Indice, 
                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' 
                        THEN ((vd.Cantidad * vd.Precio) * -1)
                        ELSE (vd.Cantidad * vd.Precio)
                    END
                ), 0) AS Total
            FROM VentaD vd
            JOIN Venta v ON vd.ID = v.ID
            WHERE 
                v.Estatus = 'CONCLUIDO'
                {filtroSucursal}
                AND v.FechaEmision >= @Inicio 
                AND v.FechaEmision < DATEADD(day, 1, @Fin)
                AND v.Mov NOT LIKE '%Pedido%'
                AND v.Mov NOT LIKE '%Venta Perdida%'
                AND v.Mov NOT LIKE '%Cotiza%'
                AND v.Mov NOT LIKE '%Carta Porte%'
            GROUP BY {agrupador}
            ORDER BY {agrupador}";

            var parametros = new { Sucursal = sucursalId, Inicio = inicio, Fin = fin };

            return await _sqlHelper.QueryAsync<GraficoPuntoModel>(query, parametros);
        }

        public async Task<KpiClienteModel> ObtenerKpisCliente(string nombreCliente, int anio, int sucursalId)
        {
            string query = @"
            SELECT 
                COUNT(DISTINCT v.MovID) as FrecuenciaCompra,
                MAX(v.FechaEmision) as UltimaCompra,
                ISNULL(SUM(
                    CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
                        ELSE (vd.Cantidad * vd.Precio)
                    END
                ), 0) AS TicketPromedio -- OJO: Traemos el Total aquí y dividimos en C#
            FROM Venta v
            JOIN VentaD vd ON v.ID = vd.ID
            JOIN Cte c ON v.Cliente = c.Cliente
            WHERE 
                v.Estatus = 'CONCLUIDO'
                AND v.Ejercicio = @Anio
                AND c.Nombre = @NombreCliente
                AND (@Sucursal = 0 OR vd.Sucursal = @Sucursal)
                AND v.Mov NOT LIKE '%Pedido%' 
                AND v.Mov NOT LIKE '%Cotiza%'";

            var parametros = new { Anio = anio, NombreCliente = nombreCliente, Sucursal = sucursalId };

            var resultado = await _sqlHelper.QueryAsync<dynamic>(query, parametros);
            var fila = resultado.FirstOrDefault();

            if ( fila == null ) return new KpiClienteModel();

            decimal total = ( decimal ) fila.TicketPromedio;
            int frecuencia = ( int ) fila.FrecuenciaCompra;

            return new KpiClienteModel
            {
                FrecuenciaCompra = frecuencia,
                UltimaCompra = fila.UltimaCompra != null ? ( DateTime ) fila.UltimaCompra : DateTime.MinValue,
                TicketPromedio = frecuencia > 0 ? total / frecuencia : 0
            };
        }

        public async Task<List<ProductoAnalisisModel>> ObtenerVariacionProductosCliente(string nombreCliente, DateTime inicio, DateTime fin, int sucursalId)
        {
            DateTime inicioAnt = inicio.AddYears(-1);
            DateTime finAnt = fin.AddYears(-1);

            string query = @"
            SELECT 
                vd.Articulo,
                ISNULL((SELECT TOP 1 Descripcion1 FROM Art WHERE Articulo = vd.Articulo), vd.Articulo) as Descripcion,
                
                SUM(CASE WHEN v.FechaEmision >= @Inicio AND v.FechaEmision <= @Fin THEN 
                    (CASE WHEN v.Mov LIKE '%Devoluci%n%' THEN ((vd.Cantidad * vd.Precio)*-1) ELSE (vd.Cantidad * vd.Precio) END)
                ELSE 0 END) as VentaActual,
                
                SUM(CASE WHEN v.FechaEmision >= @InicioAnt AND v.FechaEmision <= @FinAnt THEN 
                    (CASE WHEN v.Mov LIKE '%Devoluci%n%' THEN ((vd.Cantidad * vd.Precio)*-1) ELSE (vd.Cantidad * vd.Precio) END)
                ELSE 0 END) as VentaAnterior

            FROM VentaD vd
            JOIN Venta v ON vd.ID = v.ID
            JOIN Cte c ON v.Cliente = c.Cliente
            WHERE 
                v.Estatus = 'CONCLUIDO'
                AND (
                     (v.FechaEmision >= @Inicio AND v.FechaEmision <= @Fin) OR 
                     (v.FechaEmision >= @InicioAnt AND v.FechaEmision <= @FinAnt)
                    )
                AND c.Nombre = @NombreCliente
                AND (@Sucursal = 0 OR vd.Sucursal = @Sucursal)
                AND v.Mov NOT LIKE '%Pedido%'
            GROUP BY vd.Articulo
            HAVING SUM(CASE WHEN v.FechaEmision >= @Inicio AND v.FechaEmision <= @Fin THEN (vd.Cantidad * vd.Precio) ELSE 0 END) <> 0 
                OR SUM(CASE WHEN v.FechaEmision >= @InicioAnt AND v.FechaEmision <= @FinAnt THEN (vd.Cantidad * vd.Precio) ELSE 0 END) <> 0";

            var parametros = new
            {
                Inicio = inicio,
                Fin = fin,
                InicioAnt = inicioAnt,
                FinAnt = finAnt,
                NombreCliente = nombreCliente,
                Sucursal = sucursalId
            };

            return await _sqlHelper.QueryAsync<ProductoAnalisisModel>(query, parametros);
        }
    }
}