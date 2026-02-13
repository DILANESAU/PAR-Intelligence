using System;
using System.Collections.Generic;
using System.Linq; // Necesario para LINQ
using System.Text;

using WPF_PAR.Core;

namespace WPF_PAR.Services
{
    public class FilterService : ObservableObject
    {
        // --- EVENTO FALTANTE ---
        public event Action OnFiltrosCambiados;
        // -----------------------

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
                    // INVOCAR EL EVENTO AL CAMBIAR
                    OnFiltrosCambiados?.Invoke();
                }
            }
        }

        // ... (Tus propiedades de fecha, agregando OnFiltrosCambiados?.Invoke() en los setters si quieres que refresquen también)

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
                    // Opcional: OnFiltrosCambiados?.Invoke(); 
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
                    // Opcional: OnFiltrosCambiados?.Invoke(); 
                }
            }
        }

        private Dictionary<int, string> _listaSucursales;
        public Dictionary<int, string> ListaSucursales
        {
            get => _listaSucursales;
            set { _listaSucursales = value; OnPropertyChanged(); }
        }

        public FilterService(SucursalesService sucursalesService)
        {
            // (Tu lógica de constructor estaba bien, se mantiene igual)
            var todas = sucursalesService.CargarSucursales();

            // ... resto de tu lógica de permisos ...
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

            // ... fechas y default ...
            DateTime hoy = DateTime.Now;
            FechaInicio = new DateTime(hoy.Year, hoy.Month, 1);
            FechaFin = hoy;

            // Inicialización segura de SucursalId
            if ( Properties.Settings.Default.SucursalDefaultId != 0 && ListaSucursales.ContainsKey(Properties.Settings.Default.SucursalDefaultId) )
                SucursalId = Properties.Settings.Default.SucursalDefaultId;
            else if ( ListaSucursales.Count > 0 )
                SucursalId = ListaSucursales.Keys.First();
        }
    }
}