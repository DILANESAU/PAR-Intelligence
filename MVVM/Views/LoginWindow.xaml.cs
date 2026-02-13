using System.Windows;
using System.Windows.Input;

using WPF_PAR.MVVM.ViewModels;

namespace WPF_PAR.MVVM.Views
{
    public partial class LoginWindow : Window
    {
        // Constructor normal
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        // Constructor sin parámetros (a veces necesario para el diseñador XAML)
        public LoginWindow()
        {
            InitializeComponent();
        }

        // PERMITE ARRASTRAR LA VENTANA SIN BORDES
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ( e.ChangedButton == MouseButton.Left )
            {
                this.DragMove();
            }
        }
    }
}