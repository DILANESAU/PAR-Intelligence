using WPF_PAR.Converters;

namespace WPF_PAR.Core.Models
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