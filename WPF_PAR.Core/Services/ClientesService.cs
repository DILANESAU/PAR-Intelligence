using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json;

using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services
{
    public class ClientesService
    {
        private readonly SqlHelper _sqlHelper;

        public ClientesService(string connectionString)
        {
            // Este connectionString apunta a la base de datos de la Mini PC (PAR_System_DB)
            _sqlHelper = new SqlHelper(connectionString);
        }

        // 1. Obtener Datos Base (Top de Clientes y sus ventas mensuales)
        public async Task<List<ClienteAnalisisModel>> ObtenerDatosBase(int anioActual, int sucursalId)
        {
            // Consultamos la tabla de caché donde el Worker ya guardó el top de clientes del año
            string query = @"
                SELECT JsonDatosBase 
                FROM Cache_Clientes 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio";

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, new { Sucursal = sucursalId, Anio = anioActual });

            return string.IsNullOrEmpty(json)
                ? new List<ClienteAnalisisModel>()
                : JsonConvert.DeserializeObject<List<ClienteAnalisisModel>>(json);
        }

        // 2. Obtener KPIs del Cliente (Frecuencia, Ticket Promedio, Última Compra)
        public async Task<KpiClienteModel> ObtenerKpisCliente(string claveCliente, int anio, int sucursalId)
        {
            // Buscamos los KPIs específicos de ese cliente
            string query = @"
                SELECT JsonKpi 
                FROM Cache_Clientes_Detalle 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio AND ClaveCliente = @Cliente";

            var parametros = new { Sucursal = sucursalId, Anio = anio, Cliente = claveCliente };

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            return string.IsNullOrEmpty(json)
                ? new KpiClienteModel()
                : JsonConvert.DeserializeObject<KpiClienteModel>(json);
        }

        // 3. Obtener Variación de Productos (Top de productos que subieron o bajaron de ventas)
        public async Task<List<ProductoAnalisisModel>> ObtenerVariacionProductos(string claveCliente, int anioActual, int sucursalId)
        {
            // Obtenemos la lista de variaciones del producto para ese cliente
            string query = @"
                SELECT JsonVariacion 
                FROM Cache_Clientes_Detalle 
                WHERE IdSucursal = @Sucursal AND Anio = @Anio AND ClaveCliente = @Cliente";

            var parametros = new { Sucursal = sucursalId, Anio = anioActual, Cliente = claveCliente };

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            return string.IsNullOrEmpty(json)
                ? new List<ProductoAnalisisModel>()
                : JsonConvert.DeserializeObject<List<ProductoAnalisisModel>>(json);
        }
        // 4. Obtener Historial de 5 Años (Para la nueva Gráfica Dinámica)
        public async Task<List<HistoricoClienteModel>> ObtenerHistorialCliente(string claveCliente, int sucursalId)
        {
            string query = @"
                SELECT JsonHistorico 
                FROM Cache_Clientes_Detalle 
                WHERE IdSucursal = @Sucursal AND ClaveCliente = @Cliente";

            var parametros = new { Sucursal = sucursalId, Cliente = claveCliente };

            var json = await _sqlHelper.QueryFirstOrDefaultAsync<string>(query, parametros);

            return string.IsNullOrEmpty(json)
                ? new List<HistoricoClienteModel>()
                : JsonConvert.DeserializeObject<List<HistoricoClienteModel>>(json);
        }
    }
}