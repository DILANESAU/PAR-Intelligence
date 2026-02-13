using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using WPF_PAR.MVVM.Models;

using static WPF_PAR.Services.SqlHelper;

namespace WPF_PAR.Services
{
    public class ReportesService
    {
        private readonly SqlHelper _sqlHelper;

        public ReportesService()
        {
            _sqlHelper = new SqlHelper(TipoConexion.Data);
        }

        // =========================================================================
        // SECCIÓN 1: REPORTES DETALLADOS (Productos, Líneas, Familias)
        // Usado por: FamiliaViewModel
        // =========================================================================



        public async Task<List<VentaReporteModel>> ObtenerVentasBrutasRango(int sucursal, DateTime inicio, DateTime fin)
        {
            string query = @"
SELECT
    v.FechaEmision,
    vd.Sucursal,
    ISNULL((SELECT TOP 1 Nombre FROM Cte WHERE Cliente = v.Cliente), 'Cliente General') AS NombreCliente,
    v.MovID,
    v.Mov,
    vd.Articulo,

    -- 1. CANTIDAD (Volumen)
    ISNULL(SUM(
        CASE 
            -- Devoluciones: RESTAN cantidad
            WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
            
            -- CAMBIO AQUÍ: Usamos '%Bonifica%' para atrapar 'Bonifica Vta' y 'Bonificacion Vta'
            WHEN v.Mov LIKE '%Bonifica%' THEN 0
            
            ELSE vd.Cantidad
        END
    ), 0) AS CantidadTotal,

    -- 2. DINERO (Saldo)
    ISNULL(SUM(
        CASE 
            -- Devoluciones: RESTAN dinero
            WHEN v.Mov LIKE '%Devoluci%n%' THEN ((vd.Cantidad * vd.Precio) * -1)
            
            -- CAMBIO AQUÍ TAMBIÉN: '%Bonifica%' atrapa ambas variaciones
            WHEN v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
            
            ELSE (vd.Cantidad * vd.Precio)
        END
    ), 0) AS ImporteBrutoTotal,

    -- 3. DESCUENTOS
    ISNULL(SUM(
        CASE 
           -- Mantenemos consistencia con Devoluciones
           WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.DescuentoImporte * -1)
           ELSE vd.DescuentoImporte
        END
    ), 0) AS DescuentoTotal

FROM
    VentaD vd
    JOIN Venta v ON vd.ID = v.ID
WHERE
    v.Estatus = 'CONCLUIDO'
    AND vd.Sucursal = @Sucursal
    AND v.FechaEmision >= @Inicio 
    AND v.FechaEmision < DATEADD(day, 1, @Fin)
    
    -- EXCLUSIONES
    AND v.Mov NOT LIKE '%Pedido%'
    AND v.Mov NOT LIKE '%Venta Perdida%'
    AND v.Mov NOT LIKE '%Cotiza%'
    AND v.Mov NOT LIKE '%Carta Porte%'

GROUP BY
    v.FechaEmision, 
    vd.Sucursal, 
    v.Cliente, 
    v.MovID, 
    v.Mov, 
    vd.Articulo";

            var parametros = new Dictionary<string, object>
    {
        { "@Sucursal", sucursal },
        { "@Inicio", inicio },
        { "@Fin", fin }
    };

            return await _sqlHelper.QueryAsync(query, parametros, lector =>
            {
                decimal importeTotalSql = lector["ImporteBrutoTotal"] != DBNull.Value ? Convert.ToDecimal(lector["ImporteBrutoTotal"]) : 0m;
                double cantidadSql = lector["CantidadTotal"] != DBNull.Value ? Convert.ToDouble(lector["CantidadTotal"]) : 0d;
                decimal descuento = lector["DescuentoTotal"] != DBNull.Value ? Convert.ToDecimal(lector["DescuentoTotal"]) : 0m;

                decimal precioUnitarioVisual = 0;
                if ( Math.Abs(cantidadSql) > 0.001 )
                {
                    precioUnitarioVisual = Math.Abs(importeTotalSql / ( decimal ) cantidadSql);
                }
                else if ( lector["Mov"].ToString().Contains("Bonifica") )
                {
                    precioUnitarioVisual = importeTotalSql;
                }

                return new VentaReporteModel
                {
                    FechaEmision = Convert.ToDateTime(lector["FechaEmision"]),
                    Sucursal = lector["Sucursal"].ToString(),
                    MovID = lector["MovID"].ToString(),
                    Mov = lector["Mov"].ToString(),
                    Articulo = lector["Articulo"].ToString().Trim(),
                    Cliente = lector["NombreCliente"].ToString(),

                    Cantidad = cantidadSql,
                    Descuento = descuento,
                    TotalVenta = importeTotalSql,

                    PrecioUnitario = Math.Abs(precioUnitarioVisual)
                };
            });
        }

        public async Task<List<VentaReporteModel>> ObtenerHistoricoAnualPorArticulo(string ejercicio, string sucursal)
        {
            // -------------------------------------------------------------
            // CORRECCIÓN 1: LÓGICA DE FILTRO DINÁMICO
            // Si la sucursal es "0", null o vacía, la ignoramos para traer TODO.
            // -------------------------------------------------------------
            string filtroSucursal = "";
            var parametros = new Dictionary<string, object>
    {
        { "@Ejercicio", ejercicio }
    };

            if ( !string.IsNullOrEmpty(sucursal) && sucursal != "0" )
            {
                filtroSucursal = "AND vd.Sucursal = @Sucursal";
                parametros.Add("@Sucursal", sucursal);
            }

            string query = $@"
        SELECT 
            v.Periodo,
            vd.Articulo,
            ISNULL((SELECT TOP 1 Nombre FROM Cte WHERE Cliente = v.Cliente), 'Cliente General') AS NombreCliente,
            
            -- CANTIDAD
            ISNULL(SUM(
                CASE 
                    WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                    WHEN v.Mov LIKE '%Bonifica%' THEN 0
                    ELSE vd.Cantidad
                END
            ), 0) AS CantidadTotal,

            -- DINERO
            ISNULL(SUM(
                CASE 
                    WHEN v.Mov LIKE '%Devoluci%n%' THEN ((vd.Cantidad * vd.Precio) * -1)
                    WHEN v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
                    ELSE (vd.Cantidad * vd.Precio)
                END
            ), 0) AS ImporteBrutoTotal

        FROM VentaD vd
        JOIN Venta v ON vd.ID = v.ID
        WHERE 
            v.Ejercicio = @Ejercicio 
            AND v.Estatus = 'CONCLUIDO'
            {filtroSucursal}  -- <--- AQUÍ SE INYECTA EL FILTRO CORRECTAMENTE
            
            AND v.Mov NOT LIKE '%Pedido%'
            AND v.Mov NOT LIKE '%Venta Perdida%'
            AND v.Mov NOT LIKE '%Cotiza%'
            AND v.Mov NOT LIKE '%Carta Porte%'

        GROUP BY v.Periodo, vd.Articulo, v.Cliente";

            return await _sqlHelper.QueryAsync(query, parametros, lector =>
            {
                decimal importeBruto = lector["ImporteBrutoTotal"] != DBNull.Value ? Convert.ToDecimal(lector["ImporteBrutoTotal"]) : 0m;
                double cantidad = lector["CantidadTotal"] != DBNull.Value ? Convert.ToDouble(lector["CantidadTotal"]) : 0d;

                return new VentaReporteModel
                {
                    FechaEmision = new DateTime(int.Parse(ejercicio), Convert.ToInt32(lector["Periodo"]), 1),
                    Articulo = lector["Articulo"].ToString().Trim(),
                    Cliente = lector["NombreCliente"].ToString(),

                    Cantidad = cantidad,
                    TotalVenta = importeBruto,
                    LitrosUnitarios = 1,

                    PrecioUnitario = Math.Abs(cantidad) > 0.001 ? Math.Abs(importeBruto / ( decimal ) cantidad) : 0,
                    Descuento = 0
                };
            });
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
        
        -- 1. CALCULAMOS EL NETO REAL (Dinero)
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' 
                THEN ((vd.Cantidad * vd.Precio) * -1)
                ELSE (vd.Cantidad * vd.Precio)
            END
        ), 0) AS ImporteNeto,

        -- 2. CALCULAMOS LOS LITROS (Volumen) -- ¡NUEVO!
        -- Asumiendo que vd.Cantidad son piezas/litros. Si tienes una columna 'FactorLitros', úsala.
        -- Si 'vd.Cantidad' son los litros directos, usa esto:
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' 
                THEN (vd.Cantidad * -1)
                WHEN v.Mov LIKE '%Bonifica%' -- Bonificación no suele devolver producto físico, solo dinero
                THEN 0 
                ELSE vd.Cantidad
            END
        ), 0) AS LitrosTotales

    FROM VentaD vd
    JOIN Venta v ON vd.ID = v.ID
    LEFT JOIN Cte c ON v.Cliente = c.Cliente
    WHERE 
        v.Estatus = 'CONCLUIDO'
        {filtroSucursal}
        AND v.FechaEmision >= @Inicio 
        AND v.FechaEmision < DATEADD(day, 1, @Fin)
        
        -- EXCLUSIONES
        AND v.Mov NOT LIKE '%Pedido%'
        AND v.Mov NOT LIKE '%Venta Perdida%'
        AND v.Mov NOT LIKE '%Cotiza%'
        AND v.Mov NOT LIKE '%Carta Porte%'

    GROUP BY v.FechaEmision, vd.Sucursal, c.Nombre, v.MovID, v.Mov, vd.Articulo";

            var parametros = new Dictionary<string, object>
    {
        { "@Sucursal", sucursalId },
        { "@Inicio", inicio },
        { "@Fin", fin }
    };

            return await _sqlHelper.QueryAsync(query, parametros, lector => new VentaReporteModel
            {
                FechaEmision = Convert.ToDateTime(lector["FechaEmision"]),
                Sucursal = lector["Sucursal"].ToString(),
                Cliente = lector["Cliente"].ToString(),
                MovID = lector["MovID"].ToString(),
                Mov = lector["Mov"].ToString(),
                Articulo = lector["Articulo"].ToString(),
                TotalVenta = Convert.ToDecimal(lector["ImporteNeto"]),

                // ¡AHORA SÍ MAPEA LOS LITROS!
                LitrosTotal = lector["LitrosTotales"] != DBNull.Value ? Convert.ToDouble(lector["LitrosTotales"]) : 0
            });
        }

        public async Task<List<VentaReporteModel>> ObtenerVentaAnualAsync(int sucursalId, int anio)
        {
            string query = @"
             SELECT 
                v.Periodo AS Mes,
                SUM(v.PrecioTotal) AS TotalMensual
            FROM Venta v
            WHERE 
                v.Estatus = 'CONCLUIDO'
                AND v.Sucursal = @Sucursal 
                AND v.Ejercicio = @Ejercicio 
                AND v.Mov Like 'Factura%'
            GROUP BY v.Periodo
            ORDER BY v.Periodo";

            var parametros = new Dictionary<string, object>
            {
                { "@Sucursal", sucursalId },
                { "@Ejercicio", anio }
            };

            return await _sqlHelper.QueryAsync(query, parametros, lector => new VentaReporteModel
            {
                Mov = lector["Mes"].ToString(),
                Cantidad = 1,
                PrecioUnitario = Convert.ToDecimal(lector["TotalMensual"]),
                Descuento = 0,
                FechaEmision = new DateTime(anio, 1, 1)
            });
        }

        // En ReportesService.cs

        public async Task<List<GraficoPuntoModel>> ObtenerTendenciaGrafica(int sucursalId, DateTime inicio, DateTime fin, bool agruparPorMes)
        {
            string filtroSucursal = sucursalId > 0 ? "AND vd.Sucursal = @Sucursal" : "";
            string agrupador = agruparPorMes ? "MONTH(v.FechaEmision)" : "DAY(v.FechaEmision)";
            string seleccion = agruparPorMes ? "MONTH(v.FechaEmision)" : "DAY(v.FechaEmision)";

            string query = $@"
    SELECT 
        {seleccion} as IndiceTiempo,
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

            var parametros = new Dictionary<string, object>
    {
        { "@Sucursal", sucursalId },
        { "@Inicio", inicio },
        { "@Fin", fin }
    };

            return await _sqlHelper.QueryAsync(query, parametros, r => new GraficoPuntoModel
            {
                Indice = Convert.ToInt32(r["IndiceTiempo"]),
                Total = Convert.ToDecimal(r["Total"])
            });
        }
        public async Task<KpiClienteModel> ObtenerKpisCliente(string nombreCliente, int anio, int sucursalId)
        {
            // Esta consulta obtiene: Frecuencia (cuantas facturas), Última Fecha y Total Comprado
            string query = @"
    SELECT 
        COUNT(DISTINCT v.MovID) as Frecuencia,
        MAX(v.FechaEmision) as UltimaCompra,
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
                ELSE (vd.Cantidad * vd.Precio)
            END
        ), 0) AS TotalComprado
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

            var parametros = new Dictionary<string, object>
    {
        { "@Anio", anio },
        { "@NombreCliente", nombreCliente },
        { "@Sucursal", sucursalId }
    };

            var resultado = await _sqlHelper.QueryAsync(query, parametros, lector =>
            {
                decimal total = lector["TotalComprado"] != DBNull.Value ? Convert.ToDecimal(lector["TotalComprado"]) : 0;
                int frecuencia = lector["Frecuencia"] != DBNull.Value ? Convert.ToInt32(lector["Frecuencia"]) : 0;

                return new KpiClienteModel
                {
                    FrecuenciaCompra = frecuencia,
                    UltimaCompra = lector["UltimaCompra"] != DBNull.Value ? Convert.ToDateTime(lector["UltimaCompra"]) : DateTime.MinValue,
                    // Calculamos el Ticket Promedio aquí mismo
                    TicketPromedio = frecuencia > 0 ? total / frecuencia : 0
                };
            });

            return resultado.FirstOrDefault() ?? new KpiClienteModel();
        }

        // En ReportesService.cs

        public async Task<List<ProductoAnalisisModel>> ObtenerVariacionProductosCliente(string nombreCliente, DateTime inicio, DateTime fin, int sucursalId)
        {
            // Calculamos las fechas equivalentes del año anterior
            DateTime inicioAnt = inicio.AddYears(-1);
            DateTime finAnt = fin.AddYears(-1);

            string query = @"
    SELECT 
        vd.Articulo,
        ISNULL((SELECT TOP 1 Descripcion1 FROM Art WHERE Articulo = vd.Articulo), vd.Articulo) as Descripcion,
        
        -- Venta Rango Actual
        SUM(CASE WHEN v.FechaEmision >= @Inicio AND v.FechaEmision <= @Fin THEN 
            (CASE WHEN v.Mov LIKE '%Devoluci%n%' THEN ((vd.Cantidad * vd.Precio)*-1) ELSE (vd.Cantidad * vd.Precio) END)
        ELSE 0 END) as VentaActual,
        
        -- Venta Rango Anterior (Mismo periodo, año pasado)
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

            var parametros = new Dictionary<string, object>
    {
        { "@Inicio", inicio },
        { "@Fin", fin },
        { "@InicioAnt", inicioAnt },
        { "@FinAnt", finAnt },
        { "@NombreCliente", nombreCliente },
        { "@Sucursal", sucursalId }
    };

            return await _sqlHelper.QueryAsync(query, parametros, lector => new ProductoAnalisisModel
            {
                Articulo = lector["Articulo"].ToString(),
                Descripcion = lector["Descripcion"].ToString(),
                VentaActual = Convert.ToDecimal(lector["VentaActual"]),
                VentaAnterior = Convert.ToDecimal(lector["VentaAnterior"])
                // Diferencia se calcula sola en el modelo
            });
        }

    }

        public class GraficoPuntoModel { public int Indice { get; set; } public decimal Total { get; set; } }
}