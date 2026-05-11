using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WPF_PAR.Core.Services;
using WPF_PAR.Converters;

namespace WPF_PAR.Services
{
    public class FilterService : ObservableObject
    {
        public event Action OnFiltrosCambiados;
        private int _sucursalId;
        public int SucursalId
        {
            get => _sucursalId;
            set
            {
                if ( _sucursalId != value )
                {
                    _sucursalId = value;
                    OnPropertyChanged();
                    // Invocar evento al cambiar
                    OnFiltrosCambiados?.Invoke();
                }
            }
        }
        private int _sucursalSeleccionada;
        public int SucursalSeleccionada
        {
            get => _sucursalSeleccionada;
            set { _sucursalSeleccionada = value; OnPropertyChanged(); }
        }
        private DateTime _fechaInicio;
        public DateTime FechaInicio
        {
            get => _fechaInicio;
            set
            {
                if ( _fechaInicio != value )
                {
                    _fechaInicio = value;
                    OnPropertyChanged();
                    OnFiltrosCambiados?.Invoke();
                }
            }
        }
        private DateTime _fechaFin;
        public DateTime FechaFin
        {
            get => _fechaFin;
            set
            {
                if ( _fechaFin != value )
                {
                    _fechaFin = value;
                    OnPropertyChanged();
                    OnFiltrosCambiados?.Invoke();
                }
            }
        }
        private Dictionary<int, string> _listaSucursales;
        public Dictionary<int, string> ListaSucursales
        {
            get => _listaSucursales;
            set { _listaSucursales = value; OnPropertyChanged(); }
        }
        public FilterService(string connectionString)
        {
            var sucursalesService = new SucursalesService(connectionString);

            var todas = sucursalesService.CargarSucursales();

            if ( Session.UsuarioActual?.SucursalesPermitidas == null )
            {
                ListaSucursales = todas;
            }
            else
            {
                ListaSucursales = todas
                    .Where(s => Session.UsuarioActual.SucursalesPermitidas.Contains(s.Key))
                    .ToDictionary(k => k.Key, v => v.Value);
            }

            DateTime hoy = DateTime.Now;
            FechaInicio = new DateTime(hoy.Year, hoy.Month, 1);
            FechaFin = hoy;

            if ( Properties.Settings.Default.SucursalDefaultId != 0 && ListaSucursales.ContainsKey(Properties.Settings.Default.SucursalDefaultId) )
                SucursalId = Properties.Settings.Default.SucursalDefaultId;
            else if ( ListaSucursales.Count > 0 )
                SucursalId = ListaSucursales.Keys.First();
        }
    }
}