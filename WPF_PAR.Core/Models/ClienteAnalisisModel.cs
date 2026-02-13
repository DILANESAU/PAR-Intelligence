using System;
using System.Linq; // <--- ¡IMPORTANTÍSIMO PARA USAR .Sum(), .Take(), .Skip()!

namespace WPF_PAR.MVVM.Models
{
    public class ClienteAnalisisModel
    {
        public string Cliente { get; set; }
        public string Nombre { get; set; }

        // Arrays de datos (0 = Enero, 11 = Diciembre)
        public decimal[] VentasMensualesActual { get; set; }
        public decimal[] VentasMensualesAnterior { get; set; }
        public decimal TotalAnualAntYTD => VentasMensualesAnterior.Take(MesesParaCalculoTendencia).Sum();

        // NUEVO: Array para guardar los litros de cada mes
        public decimal[] LitrosMensualesActual { get; set; }

        // NUEVO: Propiedad calculada para el total
        public decimal TotalLitros => LitrosMensualesActual.Sum();

        // CONSTRUCTOR DE SEGURIDAD
        // Inicializa los arrays en ceros para evitar "NullReferenceException"
        public ClienteAnalisisModel()
        {
            VentasMensualesActual = new decimal[12];
            VentasMensualesAnterior = new decimal[12];
            LitrosMensualesActual = new decimal[12];
        }

        // --- PROPIEDADES CALCULADAS (Lectura dinámica) ---

        // 1. ANUAL
        public decimal TotalAnual => VentasMensualesActual.Sum();
        public decimal TotalAnualAnt => VentasMensualesAnterior.Sum();

        // 2. SEMESTRAL
        public decimal S1 => VentasMensualesActual.Take(6).Sum();          // Ene-Jun
        public decimal S2 => VentasMensualesActual.Skip(6).Take(6).Sum();  // Jul-Dic

        // 3. TRIMESTRAL
        public decimal T1 => VentasMensualesActual.Take(3).Sum();          // Ene-Mar
        public decimal T2 => VentasMensualesActual.Skip(3).Take(3).Sum();  // Abr-Jun
        public decimal T3 => VentasMensualesActual.Skip(6).Take(3).Sum();  // Jul-Sep
        public decimal T4 => VentasMensualesActual.Skip(9).Take(3).Sum();  // Oct-Dic

        // 4. MENSUAL (Mapeo completo para el DataGrid)
        // Nota: Los arrays empiezan en índice 0
        public decimal M01 => VentasMensualesActual[0]; // Ene
        public decimal M02 => VentasMensualesActual[1]; // Feb
        public decimal M03 => VentasMensualesActual[2]; // Mar
        public decimal M04 => VentasMensualesActual[3]; // Abr
        public decimal M05 => VentasMensualesActual[4]; // May
        public decimal M06 => VentasMensualesActual[5]; // Jun
        public decimal M07 => VentasMensualesActual[6]; // Jul
        public decimal M08 => VentasMensualesActual[7]; // Ago
        public decimal M09 => VentasMensualesActual[8]; // Sep
        public decimal M10 => VentasMensualesActual[9]; // Oct
        public decimal M11 => VentasMensualesActual[10]; // Nov
        public decimal M12 => VentasMensualesActual[11]; // Dic
        public int MesesParaCalculoTendencia { get; set; } = 12;
        // --- TENDENCIA INTELIGENTE ---
        public double VariacionPorcentual
        {
            get
            {
                // 1. Calculamos la venta actual (siempre es lo que llevamos)
                decimal actual = TotalAnual;

                // 2. Calculamos la venta anterior PERO LIMITADA a los mismos meses que llevamos este año
                // Ejemplo: Si estamos en Enero, solo suma Enero del año pasado.
                decimal anteriorAjustado = VentasMensualesAnterior.Take(MesesParaCalculoTendencia).Sum();

                if ( anteriorAjustado == 0 ) return 100; // Evitar división por cero

                return ( double ) ( ( ( actual - anteriorAjustado ) / anteriorAjustado ) * 100 );
            }
        }

        public int EstadoTendencia => VariacionPorcentual >= 1 ? 1 : ( VariacionPorcentual <= -1 ? -1 : 0 );
    }
}