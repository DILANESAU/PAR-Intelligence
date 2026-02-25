using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Agregamos el Core para encontrar SucursalesService
using WPF_PAR.Core.Services;
using WPF_PAR.Converters;

namespace WPF_PAR.Services
{
    public class FilterService : ObservableObject
    {
        // --- EVENTO FALTANTE ---
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

        // CAMBIO IMPORTANTE: Constructor recibe string para ser autónomo
        public FilterService(string connectionString)
        {
            // 1. Instanciamos el servicio aquí mismo
            var sucursalesService = new SucursalesService(connectionString);

            // 2. Cargamos las sucursales
            var todas = sucursalesService.CargarSucursales();

            // 3. Lógica de permisos (Usando Session)
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

            // 4. Fechas por defecto
            DateTime hoy = DateTime.Now;
            FechaInicio = new DateTime(hoy.Year, hoy.Month, 1);
            FechaFin = hoy;

            // 5. Inicialización de SucursalId por defecto
            if ( Properties.Settings.Default.SucursalDefaultId != 0 && ListaSucursales.ContainsKey(Properties.Settings.Default.SucursalDefaultId) )
                SucursalId = Properties.Settings.Default.SucursalDefaultId;
            else if ( ListaSucursales.Count > 0 )
                SucursalId = ListaSucursales.Keys.First();
        }
    }
}