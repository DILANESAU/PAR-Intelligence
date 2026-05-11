using System;
using System.Linq;

namespace WPF_PAR.Core.Models
{
    public class ClienteAnalisisModel
    {
        public string Cliente { get; set; }
        public string Nombre { get; set; }

        public decimal[] VentasMensualesActual { get; set; }
        public decimal[] VentasMensualesAnterior { get; set; }
        public decimal TotalAnualAntYTD => VentasMensualesAnterior.Take(MesesParaCalculoTendencia).Sum();

        public decimal[] LitrosMensualesActual { get; set; }

        public decimal TotalLitros => LitrosMensualesActual.Sum();

        public ClienteAnalisisModel()
        {
            VentasMensualesActual = new decimal[12];
            VentasMensualesAnterior = new decimal[12];
            LitrosMensualesActual = new decimal[12];
        }

        public decimal TotalAnual => VentasMensualesActual.Sum();


        public decimal VentaAnualActual
        {
            get
            {
                if (VentasMensualesActual == null) return 0;
                return VentasMensualesActual.Sum();
            }
        }

        // 2. Calcula la suma de todo el año anterior
        public decimal VentaAnualAnterior
        {
            get
            {
                if (VentasMensualesAnterior == null) return 0;
                return VentasMensualesAnterior.Sum();
            }
        }

        // 3. Calcula la diferencia porcentual para saber si ponerlo verde o rojo
        public double PorcentajeCrecimiento
        {
            get
            {
                // Si el año pasado no compró nada, y este año sí, es un crecimiento del 100%
                if (VentaAnualAnterior == 0)
                    return VentaAnualActual > 0 ? 1.0 : 0.0;

                // Fórmula matemática del crecimiento: (Actual - Anterior) / Anterior
                return (double)((VentaAnualActual - VentaAnualAnterior) / VentaAnualAnterior);
            }
        }

        public int MesesParaCalculoTendencia { get; set; } = 12;
        public double VariacionPorcentual
        {
            get
            {
                decimal actual = TotalAnual;

                decimal anteriorAjustado = VentasMensualesAnterior.Take(MesesParaCalculoTendencia).Sum();

                if ( anteriorAjustado == 0 ) return 100;

                return ( double ) ( ( ( actual - anteriorAjustado ) / anteriorAjustado ) * 100 );
            }
        }

        public int EstadoTendencia => VariacionPorcentual >= 1 ? 1 : ( VariacionPorcentual <= -1 ? -1 : 0 );
    }
}