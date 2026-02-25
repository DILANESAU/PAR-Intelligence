using System;
using System.Globalization;

namespace WPF_PAR.Core.Helpers
{
    public static class ColorHelper
    {
        public static string ObtenerColorTextoLegible(string hexFondo)
        {
            if ( string.IsNullOrEmpty(hexFondo) ) return "#000000";

            hexFondo = hexFondo.Replace("#", "");

            if ( hexFondo.Length == 6 )
            {
                int r = int.Parse(hexFondo.Substring(0, 2), NumberStyles.HexNumber);
                int g = int.Parse(hexFondo.Substring(2, 2), NumberStyles.HexNumber);
                int b = int.Parse(hexFondo.Substring(4, 2), NumberStyles.HexNumber);

                var yiq = ( ( r * 299 ) + ( g * 587 ) + ( b * 114 ) ) / 1000;

                return ( yiq >= 128 ) ? "#000000" : "#FFFFFF";
            }

            return "#000000";
        }

        public static string ObtenerHexPorValor(decimal valor)
        {
            return valor >= 0 ? "#4CAF50" : "#E53935";
        }
    }
}