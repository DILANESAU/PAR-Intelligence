using System;
using System.Collections.Generic;
using System.Text;

using WPF_PAR.MVVM.Models;

namespace WPF_PAR.Services
{
    public class ClientesLogicService
    {
        // En ClientesLogicService.cs

        public List<ClienteResumenModel> ProcesarClientes(List<VentaReporteModel> ventasActuales, List<VentaReporteModel> ventasAnteriores)
        {
            var todosLosClientes = ventasActuales.Select(x => x.Cliente)
                .Union(ventasAnteriores.Select(x => x.Cliente))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var resultado = new List<ClienteResumenModel>();

            foreach ( var cliente in todosLosClientes )
            {
                var row = new ClienteResumenModel { Nombre = cliente };

                // --- AÑO ACTUAL ---
                var vActual = ventasActuales.Where(x => x.Cliente == cliente).ToList();

                // Historia Mensual (Para la gráfica)
                for ( int i = 1; i <= 12; i++ )
                {
                    row.HistoriaMensualActual.Add(vActual.Where(x => x.FechaEmision.Month == i).Sum(v => v.TotalVenta));
                }

                row.VentaAnualActual = vActual.Sum(x => x.TotalVenta);
                row.LitrosAnualActual = vActual.Sum(x => x.LitrosTotales);

                // --- CÁLCULO DE TRIMESTRES (ACTUAL) ---
                // Q1 (Ene-Mar)
                row.VentaQ1Actual = vActual.Where(x => x.FechaEmision.Month <= 3).Sum(x => x.TotalVenta);
                row.LitrosQ1Actual = vActual.Where(x => x.FechaEmision.Month <= 3).Sum(x => x.LitrosTotales);

                // Q2 (Abr-Jun)
                row.VentaQ2Actual = vActual.Where(x => x.FechaEmision.Month >= 4 && x.FechaEmision.Month <= 6).Sum(x => x.TotalVenta);
                row.LitrosQ2Actual = vActual.Where(x => x.FechaEmision.Month >= 4 && x.FechaEmision.Month <= 6).Sum(x => x.LitrosTotales);

                // Q3 (Jul-Sep) -> ESTO FALTABA
                row.VentaQ3Actual = vActual.Where(x => x.FechaEmision.Month >= 7 && x.FechaEmision.Month <= 9).Sum(x => x.TotalVenta);
                row.LitrosQ3Actual = vActual.Where(x => x.FechaEmision.Month >= 7 && x.FechaEmision.Month <= 9).Sum(x => x.LitrosTotales);

                // Q4 (Oct-Dic) -> ESTO FALTABA
                row.VentaQ4Actual = vActual.Where(x => x.FechaEmision.Month >= 10).Sum(x => x.TotalVenta);
                row.LitrosQ4Actual = vActual.Where(x => x.FechaEmision.Month >= 10).Sum(x => x.LitrosTotales);

                // --- CÁLCULO DE SEMESTRES (ACTUAL) ---
                row.VentaS1Actual = row.VentaQ1Actual + row.VentaQ2Actual;
                row.LitrosS1Actual = row.LitrosQ1Actual + row.LitrosQ2Actual;

                row.VentaS2Actual = row.VentaQ3Actual + row.VentaQ4Actual; // -> ESTO FALTABA
                row.LitrosS2Actual = row.LitrosQ3Actual + row.LitrosQ4Actual;


                // --- AÑO ANTERIOR (COMPARATIVO) ---
                var vAnterior = ventasAnteriores.Where(x => x.Cliente == cliente).ToList();
                row.VentaAnualAnterior = vAnterior.Sum(x => x.TotalVenta);

                // Q1 Ant
                row.VentaQ1Anterior = vAnterior.Where(x => x.FechaEmision.Month <= 3).Sum(x => x.TotalVenta);
                // Q2 Ant
                row.VentaQ2Anterior = vAnterior.Where(x => x.FechaEmision.Month >= 4 && x.FechaEmision.Month <= 6).Sum(x => x.TotalVenta);
                // Q3 Ant
                row.VentaQ3Anterior = vAnterior.Where(x => x.FechaEmision.Month >= 7 && x.FechaEmision.Month <= 9).Sum(x => x.TotalVenta);
                // Q4 Ant
                row.VentaQ4Anterior = vAnterior.Where(x => x.FechaEmision.Month >= 10).Sum(x => x.TotalVenta);

                // Semestres Ant
                row.VentaS1Anterior = row.VentaQ1Anterior + row.VentaQ2Anterior;
                row.VentaS2Anterior = row.VentaQ3Anterior + row.VentaQ4Anterior;

                resultado.Add(row);
            }

            return resultado.OrderByDescending(x => x.VentaAnualActual).ToList();
        }
    }
}
