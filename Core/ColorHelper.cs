using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace WPF_PAR.Core
{
    public static class ColorHelper
    {
        public static string ObtenerColorTextto(string hexColor)
        {
			try
			{
				Color color = (Color)ColorConverter.ConvertFromString(hexColor);
                double luminancia = ( color.R * 0.299 + color.G * 0.587 + color.B * 0.114 );

                return luminancia < 130 ? "White" : "#1F1F1F";
            }
			catch
			{
				return "White";
			}
        }
    }
}
