using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Linq;

using WPF_PAR.MVVM.Models;

namespace WPF_PAR.Services
{
    public class ResultadoGrafico
    {
        public ISeries[] Series { get; set; }
        public Axis[] EjesX { get; set; }
    }
    public class ResultadoTopProductos
    {
        public ISeries[] Series { get; set; }
        public Axis[] EjesX { get; set; }
        public Axis[] EjesY { get; set; }
    }

    public class ChartService
    {
        // 1. TENDENCIAS (Líneas)
        public ResultadoGrafico GenerarTendenciaLineas(List<VentaReporteModel> datos, string periodo)
        {
            if ( datos == null || !datos.Any() )
                return new ResultadoGrafico { Series = Array.Empty<ISeries>(), EjesX = new Axis[0] };

            int mesFin = 12;
            // ... (Tu lógica de switch periodo, ANUAL, SEMESTRAL, etc. que ya tenías) ...
            // (Si necesitas el código completo de este método pídemelo, pero lo importante son los siguientes:)

            // ... (Código de tendencia abreviado para no saturar, asumo que este ya lo tenías similar) ...
            return new ResultadoGrafico { Series = Array.Empty<ISeries>(), EjesX = new Axis[0] }; // Placeholder si no cambiaste esto
        }

        // 2. TOP PRODUCTOS (Barras Horizontales) - ¡ESTE ES EL NUEVO!
        // Agregamos el parámetro 'int cantidadTop'
        public ResultadoTopProductos GenerarTopProductos(List<VentaReporteModel> datos, bool verPorLitros, int cantidadTop)
        {
            if ( datos == null || !datos.Any() )
            {
                return new ResultadoTopProductos { Series = Array.Empty<ISeries>(), EjesX = Array.Empty<Axis>(), EjesY = Array.Empty<Axis>() };
            }

            var topProductos = datos
                .GroupBy(x => x.Descripcion)
                .Select(g => new
                {
                    NombreVisual = g.Key,
                    Venta = ( double ) g.Sum(v => v.TotalVenta),
                    Litros = ( double ) g.Sum(v => v.LitrosTotales)
                })
                .OrderByDescending(x => verPorLitros ? x.Litros : x.Venta)
                .Take(cantidadTop)
                .Reverse()
                .ToList();

            ISeries[] series;
            Axis[] ejeX;

            if ( verPorLitros )
            {
                series = new ISeries[]
                {
            new RowSeries<double>
            {
                Values = topProductos.Select(x => x.Litros).ToArray(),
                Name = "Volumen",
                Fill = new SolidColorPaint(SKColors.Orange),

                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Right,
                DataLabelsFormatter = p => $"{p.Model:N0} L",
                DataLabelsSize = 12,

                // CORRECCIÓN: Usamos XToolTipLabelFormatter porque en RowSeries el valor está en X
                XToolTipLabelFormatter = point => $"{point.Model:N0} L"
            }
                };
                ejeX = new Axis[] { new Axis { IsVisible = false, Labeler = v => $"{v:N0}" } };
            }
            else
            {
                series = new ISeries[]
                {
            new RowSeries<double>
            {
                Values = topProductos.Select(x => x.Venta).ToArray(),
                Name = "Venta",
                Fill = new SolidColorPaint(SKColors.DodgerBlue),

                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Right,
                DataLabelsFormatter = p => $"{p.Model:C0}",
                DataLabelsSize = 12,

                // CORRECCIÓN: Usamos XToolTipLabelFormatter
                XToolTipLabelFormatter = point => $"{point.Model:C0}"
            }
                };
                ejeX = new Axis[] { new Axis { IsVisible = false, Labeler = v => $"{v:C0}" } };
            }

            var ejeY = new Axis[]
            {
        new Axis
        {
            Labels = topProductos.Select(x => NormalizarNombreProducto(x.NombreVisual)).ToArray(),
            LabelsPaint = new SolidColorPaint(SKColors.Black),
            TextSize = 11
        }
            };

            return new ResultadoTopProductos { Series = series, EjesX = ejeX, EjesY = ejeY };
        }

        // 3. PIE CHART (Pastel) - ¡ESTE TAMBIÉN CAMBIÓ A DOUBLE!
        public ISeries[] GenerarPieChart(List<LineaResumenModel> datos)
        {
            if ( datos == null || !datos.Any() ) return Array.Empty<ISeries>();

            return datos.Select(x => new PieSeries<double> // Usamos double
            {
                Values = new double[] { ( double ) x.VentaTotal },
                Name = x.NombreLinea,
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                DataLabelsFormatter = p => $"{p.Model:C0}",
                ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model:C0} ({point.StackedValue.Share:P1})"
            }).ToArray();
        }

        private string NormalizarNombreProducto(string nombreOriginal)
        {
            if ( string.IsNullOrEmpty(nombreOriginal) ) return "";
            string limpio = nombreOriginal.Trim();
            if ( limpio.Contains("-") ) { var partes = limpio.Split('-'); if ( partes.Length > 1 ) limpio = partes[1]; }
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(limpio.ToLower());
        }
    }
}