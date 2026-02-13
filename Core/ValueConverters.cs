using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WPF_PAR.Core.Converters
{
    // 1. BOOL A COLOR (Azul / Gris)
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ( value is bool esFuturo && esFuturo )
            {
                return Brushes.LightGray;
            }
            return new SolidColorBrush(( Color ) ColorConverter.ConvertFromString("#2196F3"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 2. BOOL A OPACIDAD
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ( value is bool esFuturo && esFuturo )
            {
                return 0.3;
            }
            return 1.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 3. TEXTO A MAYÚSCULAS (Corregido: Ya no está dentro de la clase anterior)
    public class ToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ( value is string texto )
            {
                return texto.ToUpper();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 4. MONTO A COLOR (Verde / Rojo)
    public class MontoColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ( value is decimal monto )
            {
                if ( monto < 0 )
                    return new SolidColorBrush(( Color ) ColorConverter.ConvertFromString("#E53935")); // Rojo

                return new SolidColorBrush(( Color ) ColorConverter.ConvertFromString("#4CAF50")); // Verde
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 5. BOOL A VISIBILIDAD (Con soporte para "Invert")
    public class BoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool valorBooleano = false;

            if ( value is bool b )
                valorBooleano = b;

            // Lógica de inversión
            if ( parameter != null && parameter.ToString() == "Invert" )
            {
                valorBooleano = !valorBooleano;
            }

            return valorBooleano ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}