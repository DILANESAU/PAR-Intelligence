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
        public decimal TotalAnualAnt => VentasMensualesAnterior.Sum();

        public decimal S1 => VentasMensualesActual.Take(6).Sum();         
        public decimal S2 => VentasMensualesActual.Skip(6).Take(6).Sum();

        public decimal T1 => VentasMensualesActual.Take(3).Sum();
        public decimal T2 => VentasMensualesActual.Skip(3).Take(3).Sum();
        public decimal T3 => VentasMensualesActual.Skip(6).Take(3).Sum();
        public decimal T4 => VentasMensualesActual.Skip(9).Take(3).Sum();

        public decimal M01 => VentasMensualesActual[0];
        public decimal M02 => VentasMensualesActual[1];
        public decimal M03 => VentasMensualesActual[2];
        public decimal M04 => VentasMensualesActual[3];
        public decimal M05 => VentasMensualesActual[4];
        public decimal M06 => VentasMensualesActual[5];
        public decimal M07 => VentasMensualesActual[6];
        public decimal M08 => VentasMensualesActual[7];
        public decimal M09 => VentasMensualesActual[8];
        public decimal M10 => VentasMensualesActual[9];
        public decimal M11 => VentasMensualesActual[10];
        public decimal M12 => VentasMensualesActual[11];
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