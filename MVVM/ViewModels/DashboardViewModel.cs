using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using MaterialDesignThemes.Wpf;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

using WPF_PAR.Converters;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class DashboardViewModel : ObservableObject
    {
        private readonly ReportesService _reporteServices;
        private readonly SucursalesService _sucursalesService;

        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;

        public FilterService Filters { get; }
        public ISnackbarMessageQueue ErrorMessageQueue { get; set; }

        public ObservableCollection<string> Periodos { get; set; } = new ObservableCollection<string> { "Hoy", "Esta Semana", "Este Mes", "Este Año" };

        private string _textoComparativo;
        public string TextoComparativo { get => _textoComparativo; set { _textoComparativo = value; OnPropertyChanged(); } }

        private bool _esCrecimientoPositivo;
        public bool EsCrecimientoPositivo { get => _esCrecimientoPositivo; set { _esCrecimientoPositivo = value; OnPropertyChanged(); } }

        private string _periodoSeleccionado = "Hoy";
        public string PeriodoSeleccionado
        {
            get => _periodoSeleccionado;
            set
            {
                _periodoSeleccionado = value;
                OnPropertyChanged();
                if ( !_isLoading ) CargarDatos();
            }
        }

        private decimal _kpiPrecioPromedio;
        public decimal KpiPrecioPromedio { get => _kpiPrecioPromedio; set { _kpiPrecioPromedio = value; OnPropertyChanged(); } }

        public ObservableCollection<SucursalModel> Sucursales { get; set; }

        private SucursalModel _sucursalSeleccionada;
        public SucursalModel SucursalSeleccionada
        {
            get => _sucursalSeleccionada;
            set
            {
                if ( _sucursalSeleccionada != value )
                {
                    _sucursalSeleccionada = value;
                    OnPropertyChanged();

                    if ( value != null )
                    {
                        Filters.SucursalId = value.Id;
                        _notificationService.ShowInfo($"Cambiando a: {value.Nombre}...");
                    }

                    if ( !_isLoading ) CargarDatos();
                }
            }
        }

        public RelayCommand ActualizarCommand { get; set; }

        private decimal _kpiVentas;
        public decimal KpiVentas { get => _kpiVentas; set { _kpiVentas = value; OnPropertyChanged(); } }

        private int _kpiTransacciones;
        public int KpiTransacciones { get => _kpiTransacciones; set { _kpiTransacciones = value; OnPropertyChanged(); } }

        private int _kpiClientes;
        public int KpiClientes { get => _kpiClientes; set { _kpiClientes = value; OnPropertyChanged(); } }

        private int _kpiClientesNuevos;
        public int KpiClientesNuevos { get => _kpiClientesNuevos; set { _kpiClientesNuevos = value; OnPropertyChanged(); } }

        private decimal _kpiLitros;
        public decimal KpiLitros { get => _kpiLitros; set { _kpiLitros = value; OnPropertyChanged(); } }

        private ISeries[] _seriesVentas;
        public ISeries[] SeriesVentas { get => _seriesVentas; set { _seriesVentas = value; OnPropertyChanged(); } }
        private Axis[] _ejeX;
        public Axis[] EjeX { get => _ejeX; set { _ejeX = value; OnPropertyChanged(); } }
        private Axis[] _ejeY;
        public Axis[] EjeY { get => _ejeY; set { _ejeY = value; OnPropertyChanged(); } }

        public ObservableCollection<TopProductoItem> TopProductosList { get; set; }
        public ObservableCollection<ClienteRecienteItem> UltimosClientesList { get; set; }
        public ObservableCollection<VentaReporteModel> ListaVentas { get; set; }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        public DashboardViewModel(ReportesService reporteServices, SucursalesService sucursalesService, IDialogService dialogService, INotificationService notificationService, FilterService filterService)
        {
            _reporteServices = reporteServices;
            _sucursalesService = sucursalesService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            Filters = filterService;

            TopProductosList = new ObservableCollection<TopProductoItem>();
            UltimosClientesList = new ObservableCollection<ClienteRecienteItem>();
            ListaVentas = new ObservableCollection<VentaReporteModel>();
            Sucursales = new ObservableCollection<SucursalModel>();

            CargarDatosIniciales();
            ActualizarCommand = new RelayCommand(o => CargarDatos());
            ConfigurarEjesIniciales();
        }
        private void ConfigurarEjesIniciales()
        {
            bool isDark = false;
            try { isDark = Properties.Settings.Default.IsDarkMode; } catch { }
            var colorTexto = isDark ? SKColors.White : SKColors.Gray;
            EjeX = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(colorTexto) } };
            EjeY = new Axis[] { new Axis { Labeler = v => $"{v:C0}", LabelsPaint = new SolidColorPaint(colorTexto) } };
        }
        public void CargarDatosIniciales()
        {
            if (Sucursales.Count == 0)
            {
                Sucursales.Clear();

                var diccionario = _sucursalesService.CargarSucursales();

                if (diccionario != null && Converters.Session.UsuarioActual != null)
                {
                    // Obtenemos la "mochila" de permisos del usuario actual
                    var permisos = Converters.Session.UsuarioActual.SucursalesPermitidas;

                    foreach (var item in diccionario)
                    {
                        // Si tiene permisos NULL (Es Jefe) O si la sucursal actual está en su mochila...
                        if (permisos == null || permisos.Contains(item.Key))
                        {
                            // ...entonces sí la agregamos a su combo
                            Sucursales.Add(new SucursalModel { Id = item.Key, Nombre = $"{item.Key} - {item.Value}" });
                        }
                    }
                }

                // 🛡️ PROTECCIÓN ANTI-CRASH
                if (Sucursales.Count > 0)
                {
                    int guardada = Properties.Settings.Default.SucursalDefaultId;

                    // Verificamos si la sucursal que tenía guardada está entre sus permitidas
                    var encontrada = Sucursales.FirstOrDefault(s => s.Id == guardada);

                    // Si la encontró, la usa. Si no, lo forzamos a su primera sucursal permitida.
                    SucursalSeleccionada = encontrada ?? Sucursales.First();
                }
                else
                {
                    System.Windows.MessageBox.Show("No se cargaron sucursales permitidas para este módulo.", "Aviso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }
        public async void CargarDatos()
        {
            if (IsLoading) return;
            IsLoading = true;
            _notificationService.ShowInfo("Leyendo caché del dashboard...");

            try
            {
                int sucursalId = SucursalSeleccionada?.Id ?? 0;

                DateTime fechaInicio = DateTime.Now.Date;
                DateTime fechaFin = DateTime.Now.Date;

                switch (PeriodoSeleccionado)
                {
                    case "Hoy":
                        fechaInicio = DateTime.Now.Date;
                        fechaFin = DateTime.Now.Date;
                        break;
                    case "Esta Semana":
                        int diff = (7 + (DateTime.Now.DayOfWeek - DayOfWeek.Monday)) % 7;
                        fechaInicio = DateTime.Now.AddDays(-1 * diff).Date;
                        fechaFin = DateTime.Now.Date;
                        break;
                    case "Este Mes":
                        fechaInicio = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                        int ultimoDiaMes = DateTime.DaysInMonth(fechaInicio.Year, fechaInicio.Month);
                        fechaFin = new DateTime(DateTime.Now.Year, DateTime.Now.Month, ultimoDiaMes);
                        break;
                    case "Este Año":
                        fechaInicio = new DateTime(DateTime.Now.Year, 1, 1);
                        fechaFin = new DateTime(DateTime.Now.Year, 12, 31);
                        break;
                }

                var datosGrafico = await _reporteServices.ObtenerTendenciaGrafica(sucursalId, PeriodoSeleccionado);

                var datosCrudos = await _reporteServices.ObtenerVentasDetalleAsync(sucursalId, fechaInicio, fechaFin);

                var datosParaKpis = datosCrudos
                    .Where(x => x.FechaEmision.Date >= fechaInicio && x.FechaEmision.Date <= fechaFin)
                    .ToList();

                KpiVentas = datosParaKpis.Sum(x => x.TotalVenta);
                KpiLitros = datosParaKpis.Sum(x => (decimal)x.Cantidad * x.LitrosUnitarios);
                KpiPrecioPromedio = KpiLitros > 0 ? (KpiVentas / KpiLitros) : 0;

                ListaVentas.Clear();
                foreach (var item in datosParaKpis)
                {
                    ListaVentas.Add(item);
                }

                ProcesarDatosResumen(datosParaKpis);

                ConfigurarGraficoDinamico(datosGrafico, fechaInicio, fechaFin, PeriodoSeleccionado);

                if (datosCrudos.Count == 0)
                {
                    _notificationService.ShowInfo($"El caché para esta sucursal aún no ha sido procesado.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error al leer caché: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ConfigurarGraficoDinamico(List<GraficoPuntoModel> datos, DateTime inicio, DateTime fin, string periodoTipo)
        {
            var valores = new List<decimal?>();
            var etiquetas = new List<string>();

            bool isDark = false;
            try { isDark = Properties.Settings.Default.IsDarkMode; } catch { }

            var colorTexto = isDark ? SKColors.White : SKColors.DarkSlateGray;
            var colorSeparador = isDark ? SKColors.White.WithAlpha(30) : SKColors.Gray.WithAlpha(30);

            if ( datos == null ) datos = new List<GraficoPuntoModel>();

            // Magia de relleno de ceros
            if ( periodoTipo == "Este Año" )
            {
                var nombresMeses = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
                for ( int i = 1; i <= 12; i++ )
                {
                    var dato = datos.FirstOrDefault(x => x.Indice == i);
                    valores.Add(dato?.Total ?? 0);
                    etiquetas.Add(nombresMeses[i - 1]);
                }
            }
            else if (periodoTipo == "Hoy") 
            {
                for (int i = 5; i <= 20; i++)
                {
                    var dato = datos.FirstOrDefault(x => x.Indice == i);
                    valores.Add(dato?.Total ?? 0);
                    etiquetas.Add($"{i}:00");
                }
            }
            else if ( periodoTipo == "Esta Semana" )
            {
                for ( int i = 0; i < 7; i++ )
                {
                    DateTime diaActualLoop = inicio.AddDays(i);
                    var dato = datos.FirstOrDefault(x => x.Indice == diaActualLoop.Day);
                    valores.Add(dato?.Total ?? 0);
                    etiquetas.Add(diaActualLoop.ToString("ddd"));
                }
            }
            else // Este Mes
            {
                int diasEnMes = DateTime.DaysInMonth(inicio.Year, inicio.Month);
                for ( int i = 1; i <= diasEnMes; i++ )
                {
                    var dato = datos.FirstOrDefault(x => x.Indice == i);
                    valores.Add(dato?.Total ?? 0);
                    etiquetas.Add(i.ToString());
                }
            }

            SeriesVentas = new ISeries[]
            {
                new LineSeries<decimal?>
                {
                    Name = "Litros",
                    Values = valores.ToArray(),
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue.WithAlpha(30)),
                    Stroke = new SolidColorPaint(SKColors.CornflowerBlue) { StrokeThickness = 3 },
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(isDark ? SKColors.Black : SKColors.White) { StrokeThickness = 2 },
                    LineSmoothness = 0.5
                }
            };

            EjeX = new Axis[] { new Axis { Labels = etiquetas.ToArray(), LabelsPaint = new SolidColorPaint(colorTexto), TextSize = 12 } };
            EjeY = new Axis[] { new Axis { Labeler = v => $"{v:N0} Lts", LabelsPaint = new SolidColorPaint(colorTexto) } };

            OnPropertyChanged(nameof(SeriesVentas));
            OnPropertyChanged(nameof(EjeX));
            OnPropertyChanged(nameof(EjeY));
        }
        private void ProcesarDatosResumen(List<VentaReporteModel> datos)
        {
            if (datos == null || !datos.Any())
            {
                KpiVentas = 0;
                KpiTransacciones = 0;
                KpiClientes = 0;
                TopProductosList.Clear();
                UltimosClientesList.Clear();
                return;
            }

            KpiVentas = datos.Sum(x => x.TotalVenta);
            KpiTransacciones = datos.Count;
            KpiClientes = datos.Select(x => x.Cliente).Distinct().Count();

            var topClientes = datos
                .GroupBy(x => x.Cliente)
                .Select(g => new TopProductoItem
                {
                    Nombre = string.IsNullOrEmpty(g.Key) ? "Público General" : g.Key,
                    // OJO: Si aquí te salen 0 litros, cámbialo a x.LitrosTotal como hicimos en Familias
                    Monto = (decimal)g.Sum(x => x.LitrosTotales)
                })
                .OrderByDescending(x => x.Monto)
                .Take(5)
                .ToList();

            for (int i = 0; i < topClientes.Count; i++) topClientes[i].Ranking = i + 1;

            // ✅ CORRECCIÓN 1: Vaciamos la caja actual y metemos los nuevos (WPF reacciona a esto)
            TopProductosList.Clear();
            foreach (var item in topClientes)
            {
                TopProductosList.Add(item);
            }

            var ultimos = datos
                .OrderByDescending(x => x.FechaEmision)
                .GroupBy(x => x.Cliente)
                .Select(g => g.First())
                .Take(5)
                .Select(x => new ClienteRecienteItem
                {
                    Nombre = string.IsNullOrEmpty(x.Cliente) ? "Público General" : x.Cliente,
                    Fecha = x.FechaEmision,
                    Iniciales = ObtenerIniciales(x.Cliente)
                })
                .ToList();

            // ✅ CORRECCIÓN 2: Vaciamos y rellenamos la lista de recientes
            UltimosClientesList.Clear();
            foreach (var item in ultimos)
            {
                UltimosClientesList.Add(item);
            }
        }
        private string ObtenerIniciales(string nombre)
        {
            if ( string.IsNullOrWhiteSpace(nombre) ) return "?";
            var partes = nombre.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if ( partes.Length == 0 ) return "?";
            if ( partes.Length == 1 ) return partes[0].Substring(0, Math.Min(2, partes[0].Length)).ToUpper();
            return ( partes[0][0].ToString() + partes[1][0].ToString() ).ToUpper();
        }
    }
    public class TopProductoItem { public int Ranking { get; set; } public string Nombre { get; set; } public decimal Monto { get; set; } public string MontoDisplay => $"{Monto:N0} Lts"; }
    public class ClienteRecienteItem { public string Iniciales { get; set; } public string Nombre { get; set; } public DateTime Fecha { get; set; } public string FechaDisplay => Fecha.ToString("dd MMM - hh:mm tt"); }
}