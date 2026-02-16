using System;
using System.Globalization;

namespace WPF_PAR.Core.Helpers
{
    public static class ColorHelper
    {
        // 1. MÉTODO PARA CONTRASTE (Texto Blanco o Negro según el fondo)
        // Este es el que necesitas en 'CrearTarjeta'
        public static string ObtenerColorTextoLegible(string hexFondo)
        {
            if ( string.IsNullOrEmpty(hexFondo) ) return "#000000"; // Negro por defecto

            // Quitamos el # si lo tiene
            hexFondo = hexFondo.Replace("#", "");

            if ( hexFondo.Length == 6 )
            {
                int r = int.Parse(hexFondo.Substring(0, 2), NumberStyles.HexNumber);
                int g = int.Parse(hexFondo.Substring(2, 2), NumberStyles.HexNumber);
                int b = int.Parse(hexFondo.Substring(4, 2), NumberStyles.HexNumber);

                // Fórmula de luminosidad estándar (YIQ)
                var yiq = ( ( r * 299 ) + ( g * 587 ) + ( b * 114 ) ) / 1000;

                // Si es oscuro (< 128), devuelve Blanco. Si es claro, devuelve Negro.
                return ( yiq >= 128 ) ? "#000000" : "#FFFFFF";
            }

            return "#000000";
        }

        // 2. MÉTODO PARA VALORES NUMÉRICOS (Verde/Rojo)
        // Este lo usarás para ganancias, pérdidas, diferencias, etc.
        public static string ObtenerHexPorValor(decimal valor)
        {
            // Verde si es positivo o cero, Rojo si es negativo
            return valor >= 0 ? "#4CAF50" : "#E53935";
        }
    }
}