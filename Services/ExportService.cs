using ClosedXML.Excel;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using WPF_PAR.MVVM.Models;

namespace WPF_PAR.Services
{
    public class ExportService
    {
        // Configuración inicial de QuestPDF (licencia comunitaria gratis)
        public ExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ========================================================================
        // 1. EXPORTAR A EXCEL (.xlsx)
        // ========================================================================
        public void ExportarExcelVentas(List<VentaReporteModel> ventas, string rutaArchivo)
        {
            using ( var workbook = new XLWorkbook() )
            {
                var worksheet = workbook.Worksheets.Add("Reporte de Ventas");

                // 1. Encabezados
                worksheet.Cell(1, 1).Value = "Fecha";
                worksheet.Cell(1, 2).Value = "Sucursal";
                worksheet.Cell(1, 3).Value = "Cliente";
                worksheet.Cell(1, 4).Value = "Producto";
                worksheet.Cell(1, 5).Value = "Familia";
                worksheet.Cell(1, 6).Value = "Línea";
                worksheet.Cell(1, 7).Value = "Litros";
                worksheet.Cell(1, 8).Value = "Importe";

                // Estilo del encabezado (Fondo azul, letras blancas, negrita)
                var rangoHeader = worksheet.Range("A1:H1");
                rangoHeader.Style.Font.Bold = true;
                rangoHeader.Style.Font.FontColor = XLColor.White;
                rangoHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                rangoHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // 2. Datos
                int row = 2;
                foreach ( var v in ventas )
                {
                    worksheet.Cell(row, 1).Value = v.FechaEmision;
                    worksheet.Cell(row, 2).Value = v.Sucursal;
                    worksheet.Cell(row, 3).Value = v.Cliente;
                    worksheet.Cell(row, 4).Value = v.Descripcion;
                    worksheet.Cell(row, 5).Value = v.Familia;
                    worksheet.Cell(row, 6).Value = v.Linea;
                    worksheet.Cell(row, 7).Value = v.LitrosTotales;
                    worksheet.Cell(row, 8).Value = v.TotalVenta;
                    row++;
                }

                // 3. Formato de Celdas
                worksheet.Column(8).Style.NumberFormat.Format = "$ #,##0.00"; // Moneda
                worksheet.Column(7).Style.NumberFormat.Format = "#,##0.00";   // Litros

                // 4. Ajustar ancho automático
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(rutaArchivo);
            }
        }

        // ========================================================================
        // 2. EXPORTAR A PDF (Reporte Ejecutivo)
        // ========================================================================
        // Método Mejorado con TODA la información
        public void ExportarPdfCliente(
            ClienteResumenModel cliente,
            KpiClienteModel kpis,
            List<VentaReporteModel> movimientos,
            List<ProductoAnalisisModel> topAumento,  // <--- NUEVO
            List<ProductoAnalisisModel> topDeclive,  // <--- NUEVO
            string rutaArchivo)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre); // Margen un poco más chico para que quepa todo
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // --- HEADER ---
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("REPORTE EJECUTIVO DE CLIENTE").Bold().FontSize(12).FontColor(Colors.Grey.Medium);
                            col.Item().Text(cliente.Nombre).Black().FontSize(20).FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9);
                        });

                        // Si tienes logo, descomenta esto:
                        // row.ConstantItem(100).Image("Assets/logo.png");
                    });

                    // --- CONTENIDO ---
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // 1. SECCIÓN DE KPIs (Resaltados)
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Component(new KpiComponent("Venta Anual", $"{cliente.VentaAnualActual:C2}"));
                            row.RelativeItem().Component(new KpiComponent("Ticket Promedio", $"{kpis.TicketPromedio:C2}"));
                            row.RelativeItem().Component(new KpiComponent("Frecuencia", $"{kpis.FrecuenciaCompra} Facturas"));
                            row.RelativeItem().Component(new KpiComponent("Última Compra", $"{kpis.UltimaCompra:dd/MMM/yyyy}"));
                        });

                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        // 2. SECCIÓN DE OPORTUNIDADES (Aumento / Declive)
                        col.Item().PaddingBottom(5).Text("Análisis de Variación de Productos (vs Año Anterior)").Bold().FontSize(12);

                        col.Item().Row(row =>
                        {
                            // Tabla Izquierda: PRODUCTOS EN DECLIVE (Riesgo)
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("📉 Productos en Riesgo (Bajan)").Bold().FontColor(Colors.Red.Medium);
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(1); });
                                    foreach ( var p in topDeclive.Take(5) )
                                    {
                                        table.Cell().Text(p.Descripcion).FontSize(9);
                                        table.Cell().AlignRight().Text($"{p.Diferencia:C0}").FontColor(Colors.Red.Medium).FontSize(9);
                                    }
                                });
                            });

                            row.ConstantItem(20); // Espacio

                            // Tabla Derecha: PRODUCTOS EN AUMENTO (Oportunidad)
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("📈 Productos en Crecimiento (Suben)").Bold().FontColor(Colors.Green.Medium);
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(1); });
                                    foreach ( var p in topAumento.Take(5) )
                                    {
                                        table.Cell().Text(p.Descripcion).FontSize(9);
                                        table.Cell().AlignRight().Text($"+{p.Diferencia:C0}").FontColor(Colors.Green.Medium).FontSize(9);
                                    }
                                });
                            });
                        });

                        col.Item().PaddingVertical(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        // 3. DETALLE DE ÚLTIMOS MOVIMIENTOS
                        col.Item().PaddingBottom(5).Text("Últimos Movimientos Registrados").Bold().FontSize(12);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70); // Fecha
                                columns.RelativeColumn(3);  // Producto
                                columns.RelativeColumn(1);  // Litros
                                columns.RelativeColumn(1);  // Importe
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Fecha");
                                header.Cell().Element(CellStyle).Text("Producto / Descripción");
                                header.Cell().Element(CellStyle).AlignRight().Text("Litros");
                                header.Cell().Element(CellStyle).AlignRight().Text("Importe");
                            });

                            foreach ( var item in movimientos.Take(100) ) // Top 100 para no hacer un libro
                            {
                                table.Cell().Element(RowStyle).Text($"{item.FechaEmision:dd/MM/yy}");
                                table.Cell().Element(RowStyle).Text(item.Descripcion);
                                table.Cell().Element(RowStyle).AlignRight().Text($"{item.LitrosTotales:N1}");
                                table.Cell().Element(RowStyle).AlignRight().Text($"{item.TotalVenta:C2}");
                            }

                            static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).PaddingVertical(2);
                            static IContainer RowStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(2);
                        });
                    });

                    // --- FOOTER ---
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Documento generado por PAR Intelligence - Página ");
                        x.CurrentPageNumber();
                    });
                });
            })
            .GeneratePdf(rutaArchivo);
        }

        // Componente auxiliar para las cajitas de KPI en el PDF
        private class KpiComponent : IComponent
        {
            private string Title { get; }
            private string Value { get; }

            public KpiComponent(string title, string value)
            {
                Title = title;
                Value = value;
            }

            public void Compose(IContainer container)
            {
                container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(column =>
                {
                    column.Item().Text(Title).FontSize(10).FontColor(Colors.Grey.Darken2);
                    column.Item().Text(Value).FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
                });
            }
        }
    }
}