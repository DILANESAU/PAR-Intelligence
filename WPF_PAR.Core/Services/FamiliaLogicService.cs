using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WPF_PAR.Core;
using WPF_PAR.Core.Configuration;
using WPF_PAR.Core.Helpers;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services.Interfaces;

namespace WPF_PAR.Core.Services
{
    public class FamiliaLogicService
    {
        private readonly IBusinessLogicService _businessLogic;

        public FamiliaLogicService(IBusinessLogicService businessLogic)
        {
            _businessLogic = businessLogic;
        }

        public (List<FamiliaResumenModel> Arqui, List<FamiliaResumenModel> Espe) CalcularResumenGlobal(List<VentaReporteModel> ventas)
        {
            var arq = new List<FamiliaResumenModel>();
            var esp = new List<FamiliaResumenModel>();

            decimal granTotal = ventas?.Sum(x => x.TotalVenta) ?? 1;
            if ( granTotal == 0 ) granTotal = 1;

            var grupos = ventas?.GroupBy(x => x.Familia).ToList() ?? new List<IGrouping<string, VentaReporteModel>>();

            foreach ( var nombre in ConfiguracionLineas.Arquitectonica )
            {
                arq.Add(CrearTarjeta(nombre, grupos, granTotal));
            }

            foreach ( var nombre in ConfiguracionLineas.Especializada )
            {
                esp.Add(CrearTarjeta(nombre, grupos, granTotal));
            }

            return (arq, esp);
        }

        private FamiliaResumenModel CrearTarjeta(string nombre, List<IGrouping<string, VentaReporteModel>> grupos, decimal granTotalGlobal)
        {
            var grupo = grupos.FirstOrDefault(g => g.Key == nombre);
            string color = _businessLogic.ObtenerColorFamilia(nombre);

            var modelo = new FamiliaResumenModel
            {
                NombreFamilia = nombre,
                ColorFondo = color,
                ColorTexto = ColorHelper.ObtenerColorTextoLegible(color),
                VentaTotal = 0,
                LitrosTotal = 0,
                PorcentajeParticipacion = 0,
                ProductoEstrella = "---"
            };

            if ( grupo != null )
            {
                modelo.VentaTotal = grupo.Sum(x => x.TotalVenta);

                modelo.LitrosTotal = ( double ) grupo.Sum(x => x.LitrosTotales);

                if ( granTotalGlobal > 0 )
                {
                    modelo.PorcentajeParticipacion = ( double ) ( modelo.VentaTotal / granTotalGlobal );
                }

                var top = grupo.GroupBy(g => g.Descripcion)
                               .OrderByDescending(x => x.Sum(v => v.LitrosTotales))
                               .FirstOrDefault();
                modelo.ProductoEstrella = top?.Key ?? "---";
            }

            return modelo;
        }

        public List<SubLineaPerformanceModel> CalcularDesgloseClientes(List<VentaReporteModel> ventas, string periodo)
        {
            var resultado = new List<SubLineaPerformanceModel>();
            if ( ventas == null || !ventas.Any() ) return resultado;

            var grupos = ventas.GroupBy(x => x.Linea ?? "Otros");

            int mesActual = DateTime.Now.Month;

            foreach ( var grupo in grupos )
            {
                var item = new SubLineaPerformanceModel
                {
                    Nombre = grupo.Key,
                    VentaTotal = grupo.Sum(x => x.TotalVenta),
                    LitrosTotales = grupo.Sum(x => x.LitrosTotales),
                    Bloques = new List<PeriodoBloque>()
                };

                if ( periodo == "SEMESTRAL" )
                {
                    item.Bloques.Add(CrearBloque("S1", grupo.ToList(), 1, 6, mesActual));
                    item.Bloques.Add(CrearBloque("S2", grupo.ToList(), 7, 12, mesActual));
                }
                else 
                {
                    item.Bloques.Add(CrearBloque("Q1", grupo.ToList(), 1, 3, mesActual));
                    item.Bloques.Add(CrearBloque("Q2", grupo.ToList(), 4, 6, mesActual));
                    item.Bloques.Add(CrearBloque("Q3", grupo.ToList(), 7, 9, mesActual));
                    item.Bloques.Add(CrearBloque("Q4", grupo.ToList(), 10, 12, mesActual));
                }

                resultado.Add(item);
            }

            return resultado.OrderByDescending(x => x.VentaTotal).ToList();
        }
        private PeriodoBloque CrearBloque(string etiqueta, List<VentaReporteModel> ventas, int mesInicio, int mesFin, int mesActual)
        {
            var ventasPeriodo = ventas
                .Where(x => x.FechaEmision.Month >= mesInicio && x.FechaEmision.Month <= mesFin)
                .ToList();

            bool esFuturo = mesInicio > mesActual;

            return new PeriodoBloque
            {
                Etiqueta = etiqueta,
                Valor = ventasPeriodo.Sum(x => x.TotalVenta),
                Litros = ventasPeriodo.Sum(x => x.LitrosTotales),
                EsFuturo = esFuturo
            };
        }

        public List<FamiliaResumenModel> OrdenarTarjetas(IEnumerable<FamiliaResumenModel> lista, string criterio)
        {
            if ( lista == null ) return new List<FamiliaResumenModel>();

            return criterio == "VENTA"
                ? lista.OrderByDescending(x => x.VentaTotal).ToList()
                : lista.OrderBy(x => x.NombreFamilia).ToList();
        }
        public string GenerarContenidoCSV(IEnumerable<VentaReporteModel> ventas)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Fecha,Sucursal,Movimiento,Folio,Cliente,Articulo,Cantidad,Precio Unitario,Total Venta");

            foreach ( var v in ventas )
            {
                sb.AppendLine(string.Format("{0:dd/MM/yyyy},{1},{2},{3},{4},{5},{6},{7},{8}",
                    v.FechaEmision,
                    v.Sucursal,
                    v.Mov,
                    v.MovID,
                    Sanitize(v.Cliente),
                    Sanitize(v.Descripcion ?? v.Articulo),
                    v.Cantidad,
                    v.PrecioUnitario,
                    v.TotalVenta
                ));
            }
            return sb.ToString();
        }

        private string Sanitize(string input) =>
            string.IsNullOrEmpty(input) ? "" : input.Replace(",", " ").Replace("\r", "").Replace("\n", "");
        public List<FamiliaResumenModel> ObtenerTarjetasVacias(string lineaActual)
        {
            List<string> nombres;
            if ( lineaActual == "Arquitectonica" ) nombres = ConfiguracionLineas.Arquitectonica;
            else if ( lineaActual == "Especializada" ) nombres = ConfiguracionLineas.Especializada;
            else nombres = ConfiguracionLineas.ObtenerTodas();

            return nombres.Select(nombre =>
            {
                string color = _businessLogic.ObtenerColorFamilia(nombre);
                return new FamiliaResumenModel
                {
                    NombreFamilia = nombre,
                    ColorFondo = color,
                    ColorTexto = ColorHelper.ObtenerColorTextoLegible(color),
                    VentaTotal = 0
                };
            }).ToList();
        }
    }
}