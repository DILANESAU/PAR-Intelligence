using WPF_PAR.Core; // Asegúrate de usar tu base ObservableObject

namespace WPF_PAR.MVVM.Models
{
    public class OpcionColor : ObservableObject
    {
        public string Nombre { get; set; }
        public string CodigoHex { get; set; }

        private bool _esSeleccionado;
        public bool EsSeleccionado
        {
            get => _esSeleccionado;
            set { _esSeleccionado = value; OnPropertyChanged(); }
        }
    }
}