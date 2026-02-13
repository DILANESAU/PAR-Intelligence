using MaterialDesignThemes.Wpf;

using System;
using System.Windows.Media;

namespace WPF_PAR.Services
{
    public class ThemeService
    {
        private readonly PaletteHelper _paletteHelper = new PaletteHelper();
        public static event Action<bool> ThemeChanged;
        public void SetThemeMode(bool isDark)
        {
            Theme theme = _paletteHelper.GetTheme();

            // Usamos el enum BaseTheme (Dark/Light)
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

            if ( isDark )
            {
                theme.Cards.Background = Color.FromRgb(48, 48, 48);
                theme.Background = Color.FromRgb(30, 30, 30);
            }
            else
            {
                theme.Cards.Background = Colors.White;
                theme.Background = Color.FromRgb(244, 246, 249);
            }

            _paletteHelper.SetTheme(theme);

            // Guardar preferencia
            try
            {
                Properties.Settings.Default.IsDarkMode = isDark;
                Properties.Settings.Default.Save();
            }
            catch { }
            ThemeChanged?.Invoke(isDark);
        }



    }
}