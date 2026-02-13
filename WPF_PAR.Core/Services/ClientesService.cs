using System;
using System.Collections.Generic;
using System.Linq; // Necesario para GroupBy, Select, Sum, etc.
using System.Threading.Tasks;

using WPF_PAR.MVVM.Models;

using static WPF_PAR.Services.SqlHelper;

namespace WPF_PAR.Services
{
    public class ClientesService
    {
        private readonly SqlHelper _sqlHelper;

        public ClientesService()
        {
            _sqlHelper = new SqlHelper(TipoConexion.Data);
        }

        // ==============================================================================
        // MÉTODO PRINCIPAL: TRAE TODO EL DESGLOSE MENSUAL (BASE DE LA TABLA DINÁMICA)
        // ==============================================================================
        // EN ClientesService.cs

        public async Task<List<ClienteAnalisisModel>> ObtenerDatosBase(int anioActual, int sucursalId)
        {
            int anioAnterior = anioActual - 1;
            string filtroSucursal = sucursalId > 0 ? "AND v.Sucursal = @Sucursal" : "";

            // QUERY ACTUALIZADA: Ahora hacemos JOIN con VentaD para sacar Cantidad (Litros)
            string query = $@"
    SELECT 
        v.Cliente,
        ISNULL(MAX(c.Nombre), 'Cliente General') AS Nombre,
        v.Ejercicio,
        MONTH(v.FechaEmision) as Mes,
        
        -- Sumamos el importe de las partidas para el dinero
        SUM(vd.Cantidad * vd.Precio) as TotalDinero,
        
        -- NUEVO: Sumamos la cantidad para los litros
        SUM(vd.Cantidad) as TotalLitros

    FROM Venta v
    JOIN VentaD vd ON v.ID = vd.ID -- <--- EL JOIN CLAVE
    LEFT JOIN Cte c ON v.Cliente = c.Cliente
    WHERE 
        v.Estatus = 'CONCLUIDO'
        {filtroSucursal}
        AND v.Ejercicio IN (@AnioActual, @AnioAnterior)
        AND (v.Mov LIKE 'Factura%' OR v.Mov LIKE 'Remisi%n%' OR v.Mov LIKE 'Nota%')
    GROUP BY v.Cliente, v.Ejercicio, MONTH(v.FechaEmision)";

            var parametros = new Dictionary<string, object>
    {
        { "@AnioActual", anioActual },
        { "@AnioAnterior", anioAnterior },
        { "@Sucursal", sucursalId }
    };

            var listaCruda = await _sqlHelper.QueryAsync(query, parametros, r => new
            {
                Cliente = r["Cliente"].ToString(),
                Nombre = r["Nombre"].ToString(),
                Ejercicio = Convert.ToInt32(r["Ejercicio"]),
                Mes = Convert.ToInt32(r["Mes"]),
                TotalDinero = Convert.ToDecimal(r["TotalDinero"]),
                TotalLitros = Convert.ToDecimal(r["TotalLitros"]) // <--- Mapeamos litros
            });

            // Lógica YTD (tu código existente)
            int mesesAComparar = 12;
            if ( anioActual == DateTime.Now.Year ) mesesAComparar = DateTime.Now.Month;

            var clientesAgrupados = listaCruda
                .GroupBy(x => new { x.Cliente, x.Nombre })
                .Select(g => new ClienteAnalisisModel
                {
                    Cliente = g.Key.Cliente,
                    Nombre = g.Key.Nombre,
                    MesesParaCalculoTendencia = mesesAComparar,

                    // Llenamos Dinero
                    VentasMensualesActual = Enumerable.Range(1, 12)
                        .Select(m => g.Where(x => x.Ejercicio == anioActual && x.Mes == m).Sum(v => v.TotalDinero))
                        .ToArray(),

                    VentasMensualesAnterior = Enumerable.Range(1, 12)
                        .Select(m => g.Where(x => x.Ejercicio == anioAnterior && x.Mes == m).Sum(v => v.TotalDinero))
                        .ToArray(),

                    // NUEVO: Llenamos Litros (Solo del año actual para no saturar memoria, o ambos si quieres)
                    LitrosMensualesActual = Enumerable.Range(1, 12)
                        .Select(m => g.Where(x => x.Ejercicio == anioActual && x.Mes == m).Sum(v => v.TotalLitros))
                        .ToArray()
                })
                .Where(x => x.VentasMensualesActual.Sum() > 0 || x.VentasMensualesAnterior.Sum() > 0)
                .OrderByDescending(x => x.VentasMensualesActual.Sum())
                .ToList();

            return clientesAgrupados;
        }
        // ==============================================================================
        // MÉTODO SECUNDARIO: KPIs INDIVIDUALES
        // ==============================================================================
        public async Task<KpiClienteModel> ObtenerKpisCliente(string cliente, int anio, int sucursalId)
        {
            string filtroSucursal = sucursalId > 0 ? "AND Sucursal = @Sucursal" : "";

            string query = $@"
            SELECT 
                COUNT(DISTINCT MovID) as Frecuencia,
                ISNULL(SUM(PrecioTotal), 0) as TotalVenta,
                MAX(FechaEmision) as UltimaFecha
            FROM Venta
            WHERE 
                Estatus = 'CONCLUIDO'
                AND Cliente = @Cliente
                AND Ejercicio = @Anio
                {filtroSucursal}
                AND (Mov LIKE 'Factura%' OR Mov LIKE 'Remisi%n%' OR Mov LIKE 'Nota%')";

            var parametros = new Dictionary<string, object>
            {
                { "@Cliente", cliente },
                { "@Anio", anio },
                { "@Sucursal", sucursalId }
            };

            return await _sqlHelper.QueryAsync(query, parametros, r =>
            {
                int freq = Convert.ToInt32(r["Frecuencia"]);
                decimal total = Convert.ToDecimal(r["TotalVenta"]);

                return new KpiClienteModel
                {
                    FrecuenciaCompra = freq,
                    UltimaCompra = r["UltimaFecha"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["UltimaFecha"]),
                    TicketPromedio = freq > 0 ? total / freq : 0
                };
            }).ContinueWith(t => t.Result.FirstOrDefault() ?? new KpiClienteModel());
        }

        // ==============================================================================
        // MÉTODO SECUNDARIO: PRODUCTOS TOP (Subidas y Bajadas)
        // ==============================================================================
        public async Task<List<ProductoAnalisisModel>> ObtenerVariacionProductos(string cliente, int anioActual, int sucursalId)
        {
            int anioAnterior = anioActual - 1;
            string filtroSucursal = sucursalId > 0 ? "AND v.Sucursal = @Sucursal" : "";

            // LÓGICA YTD (Year To Date)
            int mesLimite = 12;
            if ( anioActual == DateTime.Now.Year )
            {
                mesLimite = DateTime.Now.Month; // Si es 2026, corta en Enero (1)
            }

            string query = $@"
    WITH CalculoBase AS (
        SELECT 
            vd.Articulo,
            ISNULL(MAX(a.Descripcion1), MAX(vd.Articulo)) as Descripcion,
            
            -- VENTA AÑO ACTUAL (Filtrada por mes límite)
            ISNULL(SUM(CASE 
                WHEN v.Ejercicio = @AnioActual AND MONTH(v.FechaEmision) <= @MesLimite 
                THEN (vd.Cantidad * vd.Precio) ELSE 0 END), 0) AS VentaActual,

            -- VENTA AÑO ANTERIOR (Filtrada por el MISMO mes límite para ser justos)
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
    SELECT TOP 10 
        *,
        (VentaActual - VentaAnterior) AS Diferencia
    FROM CalculoBase
    WHERE (VentaActual - VentaAnterior) <> 0 
    ORDER BY ABS(VentaActual - VentaAnterior) DESC";

            var parametros = new Dictionary<string, object>
    {
        { "@Cliente", cliente },
        { "@AnioActual", anioActual },
        { "@AnioAnterior", anioAnterior },
        { "@Sucursal", sucursalId },
        { "@MesLimite", mesLimite } // <--- Parámetro Nuevo
    };

            return await _sqlHelper.QueryAsync(query, parametros, r => new ProductoAnalisisModel
            {
                Articulo = r["Articulo"].ToString(),
                Descripcion = r["Descripcion"].ToString(),
                VentaActual = Convert.ToDecimal(r["VentaActual"]),
                VentaAnterior = Convert.ToDecimal(r["VentaAnterior"])
            });
        }
    }
}