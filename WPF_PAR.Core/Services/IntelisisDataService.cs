using Dapper;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services // O el namespace donde decidas ponerlo
{
    // ⛏️ EL MINERO: Esta clase SOLO la usa el Worker para conectarse a la VPN/Intelisis
    public class IntelisisDataService
    {
        private readonly SqlHelper _sqlHelper;

        public IntelisisDataService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
        }

        // =================================================================
        // 1. LAS CONSULTAS VIEJAS DE REPORTES
        // =================================================================
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
                decimal precioUnitarioVisual = 0m;
                if ( Math.Abs(item.Cantidad) > 0.001m )
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
            string sqlWhereSucursal = (!string.IsNullOrEmpty(sucursal) && sucursal != "0")
                                      ? "AND vd.Sucursal = @Sucursal"
                                      : "";
            string query = $@"
    SELECT 
        v.Periodo,
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
                WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
                ELSE (vd.Cantidad * vd.Precio)
            END
        ), 0) AS TotalVenta,

        -- ¡RECUPERADO DE LA VERSIÓN ALPHA!
        ISNULL(SUM(
            CASE 
               WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.DescuentoImporte * -1)
               ELSE vd.DescuentoImporte
            END
        ), 0) AS Descuento

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
            foreach (var r in resultadosCrudos)
            {
                int periodo = (int)r.Periodo;
                double cant = (double)r.Cantidad;
                decimal total = (decimal)r.TotalVenta;
                decimal descuento = (decimal)r.Descuento; // Lo leemos dinámicamente
                int anio = int.Parse(ejercicio);

                listaFinal.Add(new VentaReporteModel
                {
                    FechaEmision = new DateTime(anio, periodo, 1),
                    Articulo = r.Articulo.ToString().Trim(),
                    Cliente = r.Cliente.ToString(),
                    Cantidad = (decimal)cant,
                    TotalVenta = total,
                    LitrosUnitarios = 1,
                    PrecioUnitario = Math.Abs(cant) > 0.001 ? Math.Abs(total / (decimal)cant) : 0,
                    Descuento = descuento // Asignamos el valor real en lugar de 0
                });
            }

            return listaFinal;
        }

        public async Task<List<GraficoPuntoModel>> ObtenerTendenciaGrafica(int sucursalId, DateTime inicio, DateTime fin, string tipoAgrupacion)
        {
            string filtroSucursal = sucursalId > 0 ? "AND vd.Sucursal = @Sucursal" : "";
            string agrupador = "";

            // Elegimos cómo agrupar el tiempo
            if (tipoAgrupacion == "Mes") agrupador = "MONTH(v.FechaEmision)";
            else if (tipoAgrupacion == "Dia") agrupador = "DAY(v.FechaEmision)";
            // OJO AQUÍ: Si en tu Intelisis "FechaEmision" guarda 00:00:00, cambia "v.FechaEmision" por "v.FechaRegistro" en la siguiente línea:
            else if (tipoAgrupacion == "Hora") agrupador = "DATEPART(HOUR, v.FechaRegistro)";

            string query = $@"
    SELECT 
        {agrupador} as Indice, 
        
        -- LITROS (Con regla de devoluciones)
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                WHEN v.Mov LIKE '%Bonifica%' THEN 0 
                ELSE vd.Cantidad
            END
        ), 0) AS Total
    FROM VentaD vd
    JOIN Venta v ON vd.ID = v.ID
    WHERE 
        v.Estatus = 'CONCLUIDO'
        {filtroSucursal}
        AND v.FechaEmision >= @Inicio 
        AND v.FechaEmision < DATEADD(day, 1, @Fin)
        AND (v.Mov LIKE 'Factura%' OR v.Mov LIKE 'Remisi%n%' OR v.Mov LIKE 'Nota%' OR v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%')
    GROUP BY {agrupador}
    ORDER BY {agrupador}";

            var parametros = new { Sucursal = sucursalId, Inicio = inicio, Fin = fin };
            return await _sqlHelper.QueryAsync<GraficoPuntoModel>(query, parametros);
        }

        // =================================================================
        // 2. LAS CONSULTAS VIEJAS DE CLIENTES
        // =================================================================
        public async Task<List<ClienteAnalisisModel>> ObtenerDatosBaseClientes(int anioActual, int sucursalId)
        {
            int anioAnterior = anioActual - 1;
            string filtroSucursal = sucursalId > 0 ? "AND v.Sucursal = @Sucursal" : "";

            string query = $@"
                SELECT 
                    v.Cliente,
                    ISNULL(MAX(c.Nombre), 'Cliente General') AS Nombre,
                    v.Ejercicio,
                    MONTH(v.FechaEmision) as Mes,
    
                 ISNULL(SUM(
                        CASE 
                        WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN ((vd.Cantidad * vd.Precio) * -1)
                        ELSE (vd.Cantidad * vd.Precio)
                     END
                ), 0) AS TotalDinero,
    
  
                ISNULL(SUM(
                    CASE 
                    WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                    WHEN v.Mov LIKE '%Bonifica%' THEN 0 
                    ELSE vd.Cantidad
                END
                ), 0) AS TotalLitros

                FROM Venta v
                JOIN VentaD vd ON v.ID = vd.ID 
                LEFT JOIN Cte c ON v.Cliente = c.Cliente
                    WHERE 
                    v.Estatus = 'CONCLUIDO'
                    {filtroSucursal}
                    AND v.Ejercicio IN (@AnioActual, @AnioAnterior)
                    AND (v.Mov LIKE 'Factura%' OR v.Mov LIKE 'Remisi%n%' OR v.Mov LIKE 'Nota%' OR v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%')
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

            var parametros = new { Anio = anio, NombreCliente = cliente, Sucursal = sucursalId };

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

        public async Task<List<ProductoAnalisisModel>> ObtenerVariacionProductos(string cliente, DateTime inicio, DateTime fin, int sucursalId)
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
                NombreCliente = cliente,
                Sucursal = sucursalId
            };

            return await _sqlHelper.QueryAsync<ProductoAnalisisModel>(query, parametros);
        }

        public async Task<List<VentaReporteModel>> ObtenerVentasRangoAsync(int sucursalId, DateTime inicio, DateTime fin)
        {
            string filtroSucursal = sucursalId > 0 ? "AND v.Sucursal = @Sucursal" : "";

            string query = $@"
    SELECT 
        v.FechaEmision, 
        vd.Sucursal,
        ISNULL(c.Nombre, 'Cliente General') as Cliente,
        
        -- NUEVO 1: Traemos al Agente/Vendedor
        ISNULL(a.Nombre, 'SIN AGENTE') as Agente, 
        
        v.MovID,
        v.Mov,
        vd.Articulo,
        
        -- DINERO QUE ENTRA (Ventas)
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' 
                THEN ((vd.Cantidad * vd.Precio) * (1 - (ISNULL(vd.DescuentoLinea, 0) / 100.0)))
                ELSE (vd.Cantidad * vd.Precio)
            END
        ), 0) AS TotalVenta,

        -- NUEVO 2: DINERO QUE CUESTA (Costo)
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' 
                THEN ((vd.Cantidad * ISNULL(vd.Costo, 0)) * -1)
                ELSE (vd.Cantidad * ISNULL(vd.Costo, 0))
            END
        ), 0) AS TotalCosto,

        -- VOLUMEN (Litros/Cantidad)
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                WHEN v.Mov LIKE '%Bonifica%' THEN 0 
                ELSE vd.Cantidad
            END
        ), 0) AS LitrosTotal

    FROM VentaD vd
    JOIN Venta v ON vd.ID = v.ID
    LEFT JOIN Cte c ON v.Cliente = c.Cliente
    LEFT JOIN Agente a ON vd.Agente = a.Agente -- (O v.Agente si ves nulos)
    WHERE 
        v.Estatus = 'CONCLUIDO'
        {filtroSucursal}
        AND v.FechaEmision >= @Inicio 
        AND v.FechaEmision < DATEADD(day, 1, @Fin)
        AND v.Mov NOT LIKE '%Pedido%'
        AND v.Mov NOT LIKE '%Venta Perdida%'
        AND v.Mov NOT LIKE '%Cotiza%'
        AND v.Mov NOT LIKE '%Carta Porte%'
    GROUP BY 
        v.FechaEmision, vd.Sucursal, c.Nombre, a.Nombre, v.MovID, v.Mov, vd.Articulo";

            var parametros = new { Sucursal = sucursalId, Inicio = inicio, Fin = fin };
            return await _sqlHelper.QueryAsync<VentaReporteModel>(query, parametros);
        }

        public async Task<List<VentaReporteModel>> ObtenerVentaAnualAsync(int sucursalId, int anio)
        {
            string query = @"
     SELECT 
        v.Periodo AS Mov, -- Truco: Usamos la propiedad Mov para guardar el numero de mes
        
        -- APLICAMOS LA REGLA DE DEVOLUCIONES
        ISNULL(SUM(
            CASE 
                WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN (v.PrecioTotal * -1)
                ELSE v.PrecioTotal
            END
        ), 0) AS PrecioUnitario -- Truco: Usamos PrecioUnitario para guardar el total

    FROM Venta v
    WHERE 
        v.Estatus = 'CONCLUIDO'
        AND v.Sucursal = @Sucursal 
        AND v.Ejercicio = @Ejercicio 
        -- MEJORAMOS EL FILTRO (Igual que en los clientes)
        AND (v.Mov LIKE 'Factura%' OR v.Mov LIKE 'Remisi%n%' OR v.Mov LIKE 'Nota%' OR v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%')
    GROUP BY v.Periodo
    ORDER BY v.Periodo";

            var parametros = new { Sucursal = sucursalId, Ejercicio = anio };

            var datos = await _sqlHelper.QueryAsync<VentaReporteModel>(query, parametros);

            foreach (var d in datos)
            {
                d.Cantidad = 1;
                d.Descuento = 0;
                d.FechaEmision = new DateTime(anio, 1, 1);
            }
            return datos;
        }
    }
}