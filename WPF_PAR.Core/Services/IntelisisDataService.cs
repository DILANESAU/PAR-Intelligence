using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services
{
    public class IntelisisDataService
    {
        private readonly SqlHelper _sqlHelper;
        public IntelisisDataService(string connectionString)
        {
            _sqlHelper = new SqlHelper(connectionString);
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
                WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN (((vd.Cantidad * vd.Precio) / 1.16) * -1)
                ELSE ((vd.Cantidad * vd.Precio) / 1.16)
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
        AND v.Mov NOT LIKE '%Traslado%'
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
                    Cantidad = cant,
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
                        WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN (((vd.Cantidad * vd.Precio) / 1.16) * -1)
                        ELSE ((vd.Cantidad * vd.Precio) / 1.16)
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
                        WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN (((vd.Cantidad * vd.Precio) / 1.16) * -1)
                        ELSE ((vd.Cantidad * vd.Precio) / 1.16)
                    END
                ), 0) AS TicketPromedio
            FROM Venta v
            JOIN VentaD vd ON v.ID = vd.ID
            JOIN Cte c ON v.Cliente = c.Cliente
            WHERE 
                v.Estatus = 'CONCLUIDO'
                AND v.Ejercicio = @Anio
                AND c.Cliente = @NombreCliente
                AND (@Sucursal = 0 OR vd.Sucursal = @Sucursal)
                AND v.Mov NOT LIKE '%Pedido%'
                AND v.Mov NOT LIKE '%Traslado%'
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
                    (CASE WHEN v.Mov LIKE '%Devoluci%n%' THEN (((vd.Cantidad * vd.Precio) / 1.16)*-1) ELSE ((vd.Cantidad * vd.Precio) / 1.16) END)
                ELSE 0 END) as VentaActual,
                
                SUM(CASE WHEN v.FechaEmision >= @InicioAnt AND v.FechaEmision <= @FinAnt THEN 
                    (CASE WHEN v.Mov LIKE '%Devoluci%n%' THEN (((vd.Cantidad * vd.Precio) / 1.16)*-1) ELSE ((vd.Cantidad * vd.Precio) / 1.16) END)
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
                AND c.Cliente = @NombreCliente
                AND (@Sucursal = 0 OR vd.Sucursal = @Sucursal)
                AND v.Mov NOT LIKE '%Pedido%'
                AND v.Mov NOT LIKE '%Carta Porte%'
                AND v.Mov NOT LIKE '%Traslado%'
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
    -- 💉 LA INYECCIÓN MAESTRA: Toma el día original de emisión y le suma la hora del registro
    DATEADD(hour, DATEPART(hour, COALESCE(v.FechaRegistro, v.UltimoCambio, '1900-01-01 00:00:00')), v.FechaEmision) AS FechaEmision,     
    
    vd.Sucursal,    
    ISNULL(c.Nombre, 'Cliente General') as Cliente,            
    ISNULL(a.Nombre, 'SIN AGENTE') as Agente,             
    v.MovID,    
    v.Mov,    
    vd.Articulo,            
    ISNULL(SUM(        
        CASE             
            WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%'             
            THEN (((vd.Cantidad * vd.Precio) / 1.16) * (1 - (ISNULL(vd.DescuentoLinea, 0) / 100.0)))            
            ELSE ((vd.Cantidad * vd.Precio) / 1.16)        
        END    
    ), 0) AS TotalVenta,    
    ISNULL(SUM(        
        CASE             
            WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%'             
            THEN ((vd.Cantidad * ISNULL(vd.Costo, 0)) * -1)            
            ELSE (vd.Cantidad * ISNULL(vd.Costo, 0))        
        END    
    ), 0) AS TotalCosto,    
    ISNULL(SUM(        
        CASE             
            WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)            
            WHEN v.Mov LIKE '%Bonifica%' THEN 0             
            ELSE vd.Cantidad        
        END    
    ), 0) AS Cantidad
FROM VentaD vd
JOIN Venta v ON vd.ID = v.ID
LEFT JOIN Cte c ON v.Cliente = c.Cliente
LEFT JOIN Agente a ON vd.Agente = a.Agente 
WHERE     
    v.Estatus = 'CONCLUIDO'    
    {filtroSucursal}    
    AND v.FechaEmision >= @Inicio     
    AND v.FechaEmision < DATEADD(day, 1, @Fin)    
    AND v.Mov NOT LIKE '%Pedido%'    
    AND v.Mov NOT LIKE '%Venta Perdida%'    
    AND v.Mov NOT LIKE '%Cotiza%'    
    AND v.Mov NOT LIKE '%Carta Porte%'
    AND v.Mov NOT LIKE '%Traslado%'
GROUP BY    
    v.FechaEmision, v.FechaRegistro, v.UltimoCambio, vd.Sucursal, c.Nombre, a.Nombre, v.MovID, v.Mov, vd.Articulo";

            var parametros = new { Sucursal = sucursalId, Inicio = inicio, Fin = fin };
            return await _sqlHelper.QueryAsync<VentaReporteModel>(query, parametros);
        }
        public async Task<List<ClienteHistorialCacheModel>> DescargarHistorialTodosLosClientes(int sucursalId)
        {
        // 1. LA CONSULTA MAESTRA: Trae 5 años de todos los clientes de golpe
            string query = @"
        SELECT 
            c.Cliente,
            c.Nombre AS NombreCliente,
            v.Ejercicio AS Anio,
            MONTH(v.FechaEmision) AS Mes,
        
            ISNULL(SUM(
                CASE 
                    WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN (((vd.Cantidad * vd.Precio) / 1.16) * -1)
                    ELSE ((vd.Cantidad * vd.Precio) / 1.16)
                END
            ), 0) AS Venta,
        
            ISNULL(SUM(
                CASE 
                    WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                    WHEN v.Mov LIKE '%Bonifica%' THEN 0 
                    ELSE vd.Cantidad
                END
            ), 0) AS Litros

        FROM VentaD vd
        JOIN Venta v ON vd.ID = v.ID
        JOIN Cte c ON v.Cliente = c.Cliente
        WHERE 
            v.Estatus = 'CONCLUIDO'
            AND (@Sucursal = 0 OR vd.Sucursal = @Sucursal)
            AND v.Mov NOT LIKE '%Pedido%' 
            AND v.Mov NOT LIKE '%Cotiza%'
            AND v.Mov NOT LIKE '%Carta Porte%'
            AND v.Mov NOT LIKE '%Traslado%'
            AND v.Ejercicio >= (YEAR(GETDATE()) - 4)
        GROUP BY 
            c.Cliente, c.Nombre, v.Ejercicio, MONTH(v.FechaEmision)";

            var parametros = new { Sucursal = sucursalId };
            var datosCrudos = await _sqlHelper.QueryAsync<dynamic>(query, parametros);
            var resultadoCache = new List<ClienteHistorialCacheModel>();
            var datosPorCliente = datosCrudos.GroupBy(x => (string)x.Cliente);

            foreach (var grupo in datosPorCliente)
            {
                string claveReal = grupo.Key;
                string nombreReal = (string)grupo.First().NombreCliente;

                var historialDelCliente = grupo.Select(x => new HistoricoClienteModel
                {
                    Anio = (int)x.Anio,
                    Mes = (int)x.Mes,
                    Venta = (decimal)x.Venta,
                    Litros = (double)x.Litros
                }).ToList();

                resultadoCache.Add(new ClienteHistorialCacheModel
                {
                    ClaveCliente = claveReal,
                    NombreCliente = nombreReal,
                    JsonHistorico = JsonConvert.SerializeObject(historialDelCliente)
                });
            }

            return resultadoCache;
        }
        public async Task<List<VentaReporteModel>> DescargarHistorialCrudoAsync(int sucursalId)
        {
            // 1. Agrupamos por Artículo en SQL para no traer millones de tickets (quitamos el JOIN con Art)
            string sqlIntelisis = @"
        SELECT 
            YEAR(v.FechaEmision) AS Anio,
            MONTH(v.FechaEmision) AS Mes,
            vd.Articulo,
            
            ISNULL(SUM(
                CASE 
                    WHEN v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%' THEN (((vd.Cantidad * vd.Precio) / 1.16) * -1)
                    ELSE ((vd.Cantidad * vd.Precio) / 1.16)
                END
            ), 0) AS TotalVenta,
            
            ISNULL(SUM(
                CASE 
                    WHEN v.Mov LIKE '%Devoluci%n%' THEN (vd.Cantidad * -1)
                    WHEN v.Mov LIKE '%Bonifica%' THEN 0 
                    ELSE vd.Cantidad
                END
            ), 0) AS LitrosTotales

        FROM VentaD vd
        JOIN Venta v ON vd.ID = v.ID
        WHERE 
            v.Estatus = 'CONCLUIDO'
            AND (@SucursalId = 0 OR vd.Sucursal = @SucursalId)
            AND v.FechaEmision >= DATEADD(year, -5, GETDATE())
            AND (v.Mov LIKE 'Factura%' OR v.Mov LIKE 'Remisi%n%' OR v.Mov LIKE 'Nota%' OR v.Mov LIKE '%Devoluci%n%' OR v.Mov LIKE '%Bonifica%')
        GROUP BY 
            YEAR(v.FechaEmision), 
            MONTH(v.FechaEmision), 
            vd.Articulo";

            var resultadosCrudos = await _sqlHelper.QueryAsync<dynamic>(sqlIntelisis, new { SucursalId = sucursalId });

            var listaFinal = new List<VentaReporteModel>();
            if (resultadosCrudos == null || !resultadosCrudos.Any()) return listaFinal;

            // 2. Lo empaquetamos en tu modelo oficial (Guardando el Año y Mes en FechaEmision)
            foreach (var r in resultadosCrudos)
            {
                listaFinal.Add(new VentaReporteModel
                {
                    Articulo = ((string)r.Articulo)?.Trim(),
                    FechaEmision = new DateTime((int)r.Anio, (int)r.Mes, 1),
                    TotalVenta = (decimal)r.TotalVenta,
                    LitrosTotal = (double)r.LitrosTotales
                });
            }

            return listaFinal;
        }
    }
    public class MesFamiliaHistorico
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public List<FamiliaResumenModel> Familias { get; set; }
    }
}