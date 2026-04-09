using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

using WPF_PAR.Converters;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class FamiliaViewModel : ObservableObject
    {
        // SERVICIOS
        private readonly ReportesService _reportesService;
        private readonly CatalogoService _catalogoService;
        private readonly ChartService _chartService;
        private readonly FamiliaLogicService _familiaLogic;
        private readonly SucursalesService _sucursalesService;
        private readonly CacheService _cacheService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        public FilterService Filters { get; }

        private bool _isInitialized = false;

        // COLECCIONES PRINCIPALES (Vista General)
        public ObservableCollection<FamiliaResumenModel> TarjetasFamilias { get; set; } = new ObservableCollection<FamiliaResumenModel>();
        public ObservableCollection<SucursalModel> Sucursales { get; set; } = new ObservableCollection<SucursalModel>();

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
                        if ( _isInitialized )
                        {
                            EjecutarReporteGeneral();
                            _notificationService.ShowInfo($"Cambiando a sucursal: {value.Nombre}");
                        }
                    }
                }
            }
        }

        // COLECCIONES SECUNDARIAS (Vista Detalle)
        private ObservableCollection<VentaReporteModel> _detalleVentas;
        public ObservableCollection<VentaReporteModel> DetalleVentas
        {
            get => _detalleVentas;
            set { _detalleVentas = value; OnPropertyChanged(); OnPropertyChanged(nameof(NoHayDatos)); }
        }
        public bool NoHayDatos => DetalleVentas == null || DetalleVentas.Count == 0;

        // Títulos
        public string TituloGraficoPastel { get; set; } = "Distribución";
        public string TituloBarrasClientes { get; set; } = "Top Clientes";
        public string TituloBarrasProductos { get; set; } = "Top Productos";

        private decimal _cacheVentaGlobal;
        private double _cacheLitrosGlobal;

        private string _tituloReporteCard = "📥 REPORTE COMPLETO";
        public string TituloReporteCard { get => _tituloReporteCard; set { _tituloReporteCard = value; OnPropertyChanged(); } }

        // GRÁFICOS
        public ISeries[] SeriesBarrasClientes { get; set; }
        public Axis[] EjeXBarrasClientes { get; set; }
        public Axis[] EjeYBarrasClientes { get; set; }
        public ISeries[] SeriesBarrasProductos { get; set; }
        public Axis[] EjeXBarrasProductos { get; set; }
        public Axis[] EjeYBarrasProductos { get; set; }
        public ISeries[] SeriesPastelDinero { get; set; }
        public ISeries[] SeriesPastelLitros { get; set; }
        public ISeries[] SeriesComportamientoLineas { get; set; }
        public Axis[] EjeXMensual { get; set; }
        public SolidColorPaint LegendTextPaint { get; set; }

        // CONTROL TOP N
        public ObservableCollection<int> OpcionesTop { get; } = new ObservableCollection<int> { 5, 10, 15, 20, 50 };
        private int _topSeleccionado = 5;
        public int TopSeleccionado
        {
            get => _topSeleccionado;
            set
            {
                if ( _topSeleccionado != value )
                {
                    _topSeleccionado = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AlturaGraficaDinamica));
                    ActualizarGraficosPorSubLinea();
                }
            }
        }
        public double AlturaGraficaDinamica => Math.Max(450, TopSeleccionado * 50);

        // ESTADO INTERNO
        private List<VentaReporteModel> _datosFamiliaActual; // Solo los datos crudos de la familia abierta
        private List<FamiliaResumenModel> _cacheTarjetasGlobal; // El JSON procesado de la tabla Familias

        public decimal GranTotalVenta { get; set; }
        public double GranTotalLitros { get; set; }

        private bool _verPorLitros;
        public bool VerPorLitros { get => _verPorLitros; set { _verPorLitros = value; OnPropertyChanged(); ActualizarGraficosPorSubLinea(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private bool _verResumen = true;
        public bool VerResumen { get => _verResumen; set { _verResumen = value; OnPropertyChanged(); OnPropertyChanged(nameof(VerDetalle)); } }
        public bool VerDetalle => !VerResumen;

        public string TituloDetalle { get; set; }

        private string _textoBusqueda;
        public string TextoBusqueda { get => _textoBusqueda; set { _textoBusqueda = value; OnPropertyChanged(); FiltrarTabla(); } }

        public ObservableCollection<string> SubLineasDisponibles { get; set; } = new ObservableCollection<string>();

        private string _subLineaSeleccionada;
        public string SubLineaSeleccionada { get => _subLineaSeleccionada; set { _subLineaSeleccionada = value; OnPropertyChanged(); if ( !string.IsNullOrEmpty(value) ) ActualizarGraficosPorSubLinea(); } }

        private bool _excluirBlancos;
        public bool ExcluirBlancos { get => _excluirBlancos; set { _excluirBlancos = value; OnPropertyChanged(); ActualizarGraficosPorSubLinea(); } }

        public ObservableCollection<SubLineaPerformanceModel> ListaDesglose { get; set; }

        // COMANDOS
        public RelayCommand ActualizarCommand { get; set; }
        public RelayCommand VerDetalleCommand { get; set; }
        public RelayCommand RegresarCommand { get; set; }
        public RelayCommand ExportarExcelCommand { get; set; }
        public RelayCommand CambiarPeriodoGraficoCommand { get; set; }
        public RelayCommand OrdenarVentaCommand { get; set; }
        public RelayCommand OrdenarNombreCommand { get; set; }
        public RelayCommand ExportarGlobalCommand { get; set; }

        public FamiliaViewModel(ReportesService reportesService, SucursalesService sucursalesService, CatalogoService catalogoService, ChartService chartService, FamiliaLogicService familiaLogic, CacheService cacheService, IDialogService dialogService, INotificationService notificationService, FilterService filterService)
        {
            _reportesService = reportesService;
            _sucursalesService = sucursalesService;
            _catalogoService = catalogoService;
            _chartService = chartService;
            _familiaLogic = familiaLogic;
            _cacheService = cacheService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            Filters = filterService;

            ActualizarColoresGraficos();

            ActualizarCommand = new RelayCommand(o => EjecutarReporteGeneral());
            OrdenarVentaCommand = new RelayCommand(o => AplicarOrden("VENTA"));
            OrdenarNombreCommand = new RelayCommand(o => AplicarOrden("NOMBRE"));
            RegresarCommand = new RelayCommand(o => RestaurarVistaGeneral());
            ExportarGlobalCommand = new RelayCommand(o => GenerarReporteExcel(VerResumen));
            VerDetalleCommand = new RelayCommand(param => { if ( param is string familia ) CargarDetalle(familia); });

            // Ya no usamos GenerarDesglosePorPeriodo porque la tabla histórica ya no se carga aquí para no saturar memoria.
            CambiarPeriodoGraficoCommand = new RelayCommand(param => { });

            Filters.OnFiltrosCambiados += EjecutarReporteGeneral;
        }

        private void RestaurarVistaGeneral()
        {
            VerResumen = true;
            GranTotalVenta = _cacheVentaGlobal;
            GranTotalLitros = _cacheLitrosGlobal;
            TituloReporteCard = "📥 REPORTE GLOBAL";
            OnPropertyChanged(nameof(GranTotalVenta));
            OnPropertyChanged(nameof(GranTotalLitros));
        }

        private void ActualizarColoresGraficos()
        {
            bool isDark = false;
            try { isDark = Properties.Settings.Default.IsDarkMode; } catch { }
            var colorTexto = isDark ? SKColors.White : SKColors.Black;
            var colorSeparador = isDark ? SKColors.White.WithAlpha(30) : SKColors.Gray.WithAlpha(50);
            LegendTextPaint = new SolidColorPaint(colorTexto);
            var ejeX = new Axis[] { new Axis { IsVisible = false, LabelsPaint = new SolidColorPaint(colorTexto) } };
            var ejeY = new Axis[] { new Axis { IsVisible = true, LabelsPaint = new SolidColorPaint(colorTexto), TextSize = 12, SeparatorsPaint = new SolidColorPaint(colorSeparador) } };
            EjeXBarrasClientes = ejeX; EjeYBarrasClientes = ejeY;
            EjeXBarrasProductos = ejeX; EjeYBarrasProductos = ejeY;
            EjeXMensual = new Axis[] { new Axis { LabelsPaint = new SolidColorPaint(colorTexto), SeparatorsPaint = null } };
        }

        public void CargarDatosIniciales()
        {
            if ( Sucursales.Count == 0 )
            {
                try
                {
                    Sucursales.Clear();
                    Sucursales.Add(new SucursalModel { Id = 0, Nombre = "0 - TODAS" });
                    var dic = _sucursalesService.CargarSucursales();
                    if ( dic != null )
                    {
                        foreach ( var kvp in dic )
                            Sucursales.Add(new SucursalModel { Id = kvp.Key, Nombre = $"{kvp.Key} - {kvp.Value}" });
                    }
                    int idGuardado = Properties.Settings.Default.SucursalDefaultId;
                    var encontrada = Sucursales.FirstOrDefault(s => s.Id == idGuardado);
                    SucursalSeleccionada = encontrada ?? Sucursales.First();
                }
                catch ( Exception ex )
                {
                    _notificationService.ShowError("Error cargando sucursales: " + ex.Message);
                }
            }

            if ( !_isInitialized )
            {
                EjecutarReporteGeneral();
                _isInitialized = true;
            }
        }

        // ==========================================================
        // VISTA 1: PANTALLA GENERAL (LEE CACHÉ DE FAMILIAS)
        // ==========================================================
        private async void EjecutarReporteGeneral()
        {
            IsLoading = true;
            try
            {
                // 🔥 AQUÍ ESTÁ LA MAGIA: Leemos las familias ya calculadas
                _cacheTarjetasGlobal = await _cacheService.ObtenerFamiliasAsync(Filters.SucursalId);

                if ( _cacheTarjetasGlobal == null || _cacheTarjetasGlobal.Count == 0 )
                {
                    _notificationService.ShowInfo("El worker aún no genera el resumen de familias.");
                    TarjetasFamilias.Clear();
                    GranTotalVenta = 0;
                    GranTotalLitros = 0;
                    return;
                }

                _cacheVentaGlobal = _cacheTarjetasGlobal.Sum(x => x.VentaTotal);
                _cacheLitrosGlobal = _cacheTarjetasGlobal.Sum(x => x.LitrosTotales);

                GranTotalVenta = _cacheVentaGlobal;
                GranTotalLitros = _cacheLitrosGlobal;
                TituloReporteCard = "📥 REPORTE GLOBAL";

                TarjetasFamilias = new ObservableCollection<FamiliaResumenModel>(_cacheTarjetasGlobal.OrderByDescending(x => x.LitrosTotal));

                OnPropertyChanged(nameof(GranTotalVenta));
                OnPropertyChanged(nameof(GranTotalLitros));
                OnPropertyChanged(nameof(TarjetasFamilias));
            }
            catch ( Exception ex )
            {
                await _notificationService.ShowErrorDialog($"Error cargando familias: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ==========================================================
        // VISTA 2: PANTALLA DETALLE (LEE CACHÉ DE VENTAS Y FILTRA)
        // ==========================================================
        private async void CargarDetalle(string familia)
        {
            IsLoading = true;
            try
            {
                TituloDetalle = familia;

                // 1. Descargamos TODAS las ventas del mes (el JSON grande)
                var ventasCompletas = await _cacheService.ObtenerVentasDetalleAsync(Filters.SucursalId);

                // 2. Filtramos SOLO las que corresponden a la familia seleccionada
                _datosFamiliaActual = ventasCompletas.Where(x => x.Familia == familia).ToList();

                GranTotalVenta = _datosFamiliaActual.Sum(x => x.TotalVenta);
                GranTotalLitros = (double)_datosFamiliaActual.Sum(x => x.LitrosTotales);
                TituloReporteCard = "📥 DESCARGAR DETALLE";

                OnPropertyChanged(nameof(GranTotalVenta));
                OnPropertyChanged(nameof(GranTotalLitros));

                // 3. Llenamos el combo de sublíneas
                SubLineasDisponibles.Clear();
                SubLineasDisponibles.Add("TODAS");
                var lineas = _datosFamiliaActual.Select(x => x.Linea).Distinct().OrderBy(x => x).ToList();
                foreach ( var l in lineas ) SubLineasDisponibles.Add(l);

                SubLineaSeleccionada = "TODAS";
                OnPropertyChanged(nameof(TituloDetalle));

                VerResumen = false;
                ActualizarGraficosPorSubLinea();
            }
            catch ( Exception ex )
            {
                _notificationService.ShowError("Error al cargar detalle: " + ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ActualizarGraficosPorSubLinea()
        {
            if ( _datosFamiliaActual == null ) return;

            // Filtramos la vista
            string filtro = SubLineaSeleccionada;
            bool esVistaGlobal = ( filtro == "TODAS" || string.IsNullOrEmpty(filtro) );

            var datosFiltrados = esVistaGlobal
                ? _datosFamiliaActual.ToList()
                : _datosFamiliaActual.Where(x => ( x.Linea ?? "" ).Equals(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

            DetalleVentas = new ObservableCollection<VentaReporteModel>(datosFiltrados.OrderByDescending(x => x.TotalVenta));

            // Generamos las gráficas de pastel
            TituloGraficoPastel = esVistaGlobal ? "Distribución por Línea" : "Distribución por Producto";

            var resumenBase = esVistaGlobal
                ? datosFiltrados.GroupBy(x => x.Linea)
                : datosFiltrados.GroupBy(x => x.Descripcion);

            var resumenDatos = resumenBase.Select(g => new
            {
                Nombre = g.Key ?? "SIN ESPECIFICAR",
                Venta = g.Sum(x => x.TotalVenta),
                Litros = g.Sum(x => x.LitrosTotales) // <--- Aquí usamos la calculada
            }).ToList();

            SeriesPastelDinero = resumenDatos.OrderByDescending(x => x.Venta).Take(5).Select(x => new PieSeries<double>
            {
                Values = new double[] { ( double ) x.Venta },
                Name = NormalizarNombreProducto(x.Nombre),
                InnerRadius = 0,
                DataLabelsFormatter = p => $"{p.Model:C0} ({p.StackedValue.Share:P0})",
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsSize = 11,
                ToolTipLabelFormatter = p => $"{p.Context.Series.Name}: {p.Model:C0} ({p.StackedValue.Share:P1})"
            }).ToArray();

            SeriesPastelLitros = resumenDatos.OrderByDescending(x => x.Litros).Take(5).Select(x => new PieSeries<double>
            {
                Values = new double[] { (double)x.Litros },
                Name = NormalizarNombreProducto(x.Nombre),
                InnerRadius = 0,
                DataLabelsFormatter = p => $"{p.Model:N0} L ({p.StackedValue.Share:P0})",
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsSize = 11,
                ToolTipLabelFormatter = p => $"{p.Context.Series.Name}: {p.Model:N0} L ({p.StackedValue.Share:P1})"
            }).ToArray();

            // GRÁFICA DE CLIENTES
            var datosParaClientes = datosFiltrados.Select(x => new VentaReporteModel
            {
                Descripcion = string.IsNullOrEmpty(x.Cliente) ? "PÚBLICO GENERAL" : x.Cliente,
                TotalVenta = x.TotalVenta,
                LitrosTotal = (double)x.LitrosTotales // <--- ChartService probablemente usa esta para gráficas
            }).ToList();

            var resClientes = _chartService.GenerarTopProductos(datosParaClientes, VerPorLitros, TopSeleccionado);
            SeriesBarrasClientes = resClientes.Series;
            EjeXBarrasClientes = resClientes.EjesX;
            EjeYBarrasClientes = resClientes.EjesY;
            TituloBarrasClientes = $"Top {TopSeleccionado} Clientes";

            // GRÁFICA DE PRODUCTOS
            var datosParaProductos = datosFiltrados.ToList();
            if ( ExcluirBlancos )
            {
                datosParaProductos = datosParaProductos
                    .Where(x => !x.Descripcion.ToUpper().Contains("BLANCO") && !x.Descripcion.ToUpper().Contains(" BCO ") && !x.Descripcion.ToUpper().EndsWith(" BCO"))
                    .ToList();
                TituloBarrasProductos = $"Top {TopSeleccionado} Productos (Sin Blancos)";
            }
            else
            {
                TituloBarrasProductos = $"Top {TopSeleccionado} Productos";
            }

            var resProductos = _chartService.GenerarTopProductos(datosParaProductos, VerPorLitros, TopSeleccionado);
            SeriesBarrasProductos = resProductos.Series;
            EjeXBarrasProductos = resProductos.EjesX;
            EjeYBarrasProductos = resProductos.EjesY;

            OnPropertyChanged(nameof(SeriesPastelDinero));
            OnPropertyChanged(nameof(SeriesPastelLitros));
            OnPropertyChanged(nameof(TituloGraficoPastel));
            OnPropertyChanged(nameof(SeriesBarrasClientes));
            OnPropertyChanged(nameof(EjeXBarrasClientes));
            OnPropertyChanged(nameof(EjeYBarrasClientes));
            OnPropertyChanged(nameof(TituloBarrasClientes));
            OnPropertyChanged(nameof(SeriesBarrasProductos));
            OnPropertyChanged(nameof(EjeXBarrasProductos));
            OnPropertyChanged(nameof(EjeYBarrasProductos));
            OnPropertyChanged(nameof(TituloBarrasProductos));
        }

        private string NormalizarNombreProducto(string n)
        {
            if ( string.IsNullOrEmpty(n) ) return "";
            string l = n.Trim();
            if ( l.Contains("-") )
            {
                var p = l.Split('-');
                if ( p.Length > 1 ) l = p[1];
            }
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(l.ToLower());
        }

        private void AplicarOrden(string criterio)
        {
            if ( TarjetasFamilias != null )
            {
                TarjetasFamilias = new ObservableCollection<FamiliaResumenModel>(_familiaLogic.OrdenarTarjetas(TarjetasFamilias.ToList(), criterio));
                OnPropertyChanged(nameof(TarjetasFamilias));
            }
        }

        private async void GenerarReporteExcel(bool esGlobal)
        {
            var datosParaExportar = esGlobal ? _detalleVentas?.ToList() : DetalleVentas?.ToList();

            if ( datosParaExportar == null || !datosParaExportar.Any() )
            {
                _notificationService.ShowInfo("No hay datos para exportar.");
                return;
            }

            string path = _dialogService.ShowSaveFileDialog("Excel|*.xlsx", $"Reporte_{DateTime.Now:yyyyMMdd}.xlsx");
            if ( !string.IsNullOrEmpty(path) )
            {
                IsLoading = true;
                await Task.Run(() =>
                {
                    var exporter = new ExportService();
                    exporter.ExportarExcelVentas(datosParaExportar, path);
                });
                IsLoading = false;
            }
        }

        private void FiltrarTabla()
        {
            if ( DetalleVentas != null )
            {
                var v = CollectionViewSource.GetDefaultView(DetalleVentas);
                if ( string.IsNullOrWhiteSpace(TextoBusqueda) ) v.Filter = null;
                else
                {
                    string t = TextoBusqueda.ToUpper();
                    v.Filter = o =>
                    {
                        if ( o is VentaReporteModel m )
                            return ( m.Cliente?.ToUpper().Contains(t) ?? false ) || ( m.Descripcion?.ToUpper().Contains(t) ?? false );
                        return false;
                    };
                }
            }
        }
    }
}