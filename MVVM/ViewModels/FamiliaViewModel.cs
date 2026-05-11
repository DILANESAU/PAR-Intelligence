using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Measure;
using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using Newtonsoft.Json;
using WPF_PAR.Converters;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class FamiliaViewModel : ObservableObject
    {
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

        private ObservableCollection<FamiliaResumenModel> _tarjetasFamilias;
        public ObservableCollection<FamiliaResumenModel> TarjetasFamilias
        {
            get => _tarjetasFamilias;
            set
            {
                _tarjetasFamilias = value;
                OnPropertyChanged();
            }
        }
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
        private ObservableCollection<VentaReporteModel> _detalleVentas;
        public ObservableCollection<VentaReporteModel> DetalleVentas
        {
            get => _detalleVentas;
            set { _detalleVentas = value; OnPropertyChanged(); OnPropertyChanged(nameof(NoHayDatos)); }
        }
        public bool NoHayDatos => DetalleVentas == null || DetalleVentas.Count == 0;
        public string TituloGraficoPastel { get; set; } = "Distribución";
        private decimal _cacheVentaGlobal;
        private double _cacheLitrosGlobal;
        private string _tituloReporteCard = "📥 REPORTE COMPLETO";
        public string TituloReporteCard { get => _tituloReporteCard; set { _tituloReporteCard = value; OnPropertyChanged(); } }

        private ObservableCollection<ISeries> _seriesBarrasDinero;
        public ObservableCollection<ISeries> SeriesBarrasDinero
        {
            get => _seriesBarrasDinero;
            set { _seriesBarrasDinero = value; OnPropertyChanged(); }
        }
        private Axis[] _ejeYLineasDinero;
        public Axis[] EjeYLineasDinero
        {
            get => _ejeYLineasDinero;
            set { _ejeYLineasDinero = value; OnPropertyChanged(); }
        }
        private ObservableCollection<ISeries> _seriesBarrasLitros;
        public ObservableCollection<ISeries> SeriesBarrasLitros
        {
            get => _seriesBarrasLitros;
            set { _seriesBarrasLitros = value; OnPropertyChanged(); }
        }
        private ObservableCollection<ISeries> _seriesEvolucionFamilias;
        public ObservableCollection<ISeries> SeriesEvolucionFamilias
        {
            get => _seriesEvolucionFamilias;
            set { _seriesEvolucionFamilias = value; OnPropertyChanged(); }
        }

        private Axis[] _ejeXMeses;
        public Axis[] EjeXMeses
        {
            get => _ejeXMeses;
            set { _ejeXMeses = value; OnPropertyChanged(); }
        }
        private Axis[] _ejeYLineasLitros;
        public Axis[] EjeYLineasLitros
        {
            get => _ejeYLineasLitros;
            set { _ejeYLineasLitros = value; OnPropertyChanged(); }
        }
        public Axis[] EjeXBarrasClientes { get; set; }
        private Axis[] _ejeXValores;
        public Axis[] EjeXValores
        {
            get => _ejeXValores;
            set { _ejeXValores = value; OnPropertyChanged(); }
        }
        public Axis[] EjeYBarrasClientes { get; set; }
        public ISeries[] SeriesBarrasProductos { get; set; }
        public Axis[] EjeXBarrasProductos { get; set; }
        public Axis[] EjeYBarrasProductos { get; set; }
        public ISeries[] SeriesComportamientoLineas { get; set; }
        public Axis[] EjeXMensual { get; set; }
        public SolidColorPaint LegendTextPaint { get; set; }

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

        private List<VentaReporteModel> _datosFamiliaActual;
        private List<FamiliaResumenModel> _cacheTarjetasGlobal; 
        private decimal _granTotalVenta;

        public decimal GranTotalVenta
        {
            get => _granTotalVenta;
            set
            {
                _granTotalVenta = value;
                OnPropertyChanged();
            }
        }
        private double _granTotalLitros;
        public double GranTotalLitros
        {
            get => _granTotalLitros;
            set
            {
                _granTotalLitros = value;
                OnPropertyChanged();
            }
        }
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
        public ObservableCollection<SubLineaPerformanceModel> ListaDesglose { get; set; } 
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

            CambiarPeriodoGraficoCommand = new RelayCommand(param => { });

            Filters.OnFiltrosCambiados += () =>
            {
                if (VerResumen)
                {
                    EjecutarReporteGeneral();
                }
                else
                {
                    EjecutarReporteGeneral();
                    CargarDetalle(familia: TituloDetalle);
                }
            };
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
            if (Sucursales.Count == 0)
            {
                Sucursales.Clear();

                var diccionario = _sucursalesService.CargarSucursales();

                if (diccionario != null && Converters.Session.UsuarioActual != null)
                {
                    var permisos = Converters.Session.UsuarioActual.SucursalesPermitidas;

                    foreach (var item in diccionario)
                    {
                        if (permisos == null || permisos.Contains(item.Key))
                        {
                            Sucursales.Add(new SucursalModel { Id = item.Key, Nombre = $"{item.Key} - {item.Value}" });
                        }
                    }
                }
                if (Sucursales.Count > 0)
                {
                    int guardada = Properties.Settings.Default.SucursalDefaultId;
                    var encontrada = Sucursales.FirstOrDefault(s => s.Id == guardada);
                    SucursalSeleccionada = encontrada ?? Sucursales.First();
                }
                else
                {
                    System.Windows.MessageBox.Show("No se cargaron sucursales permitidas para este módulo.", "Aviso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }

            if ( !_isInitialized )
            {
                EjecutarReporteGeneral();
                _isInitialized = true;
            }
        }
        private async void EjecutarReporteGeneral()
        {
            IsLoading = true;
            try
            {
                // 1. Calculamos las fechas de forma segura para la base de datos
                DateTime fechaSeguraInicio = Filters.FechaInicio != DateTime.MinValue
                    ? Filters.FechaInicio.Date
                    : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                DateTime fechaSeguraFin = Filters.FechaFin != DateTime.MinValue
                    ? Filters.FechaFin.Date
                    : DateTime.Now.Date;

                // 2. Traemos los BLOQUES MENSUALES completos desde la BD
                var ventasBloquesMensuales = await _reportesService.ObtenerVentasDetalleAsync(Filters.SucursalId, fechaSeguraInicio, fechaSeguraFin);

                if (ventasBloquesMensuales == null || !ventasBloquesMensuales.Any())
                {
                    _notificationService.ShowInfo("No hay ventas registradas para este periodo.");
                    GranTotalVenta = 0;
                    GranTotalLitros = 0;
                    return;
                }

                // 🟢 3. EL FILTRO ESTRICTO: Recortamos los días exactos que pidió el usuario
                var ventasCompletas = ventasBloquesMensuales
                    .Where(v => v.FechaEmision.Date >= fechaSeguraInicio && v.FechaEmision.Date <= fechaSeguraFin)
                    .ToList();

                // 4. Alimentamos la gráfica DIRECTAMENTE con los datos estrictamente filtrados
                GenerarGraficaEvolucion(ventasCompletas);

                // 5. Calculamos el resumen global para las tarjetas (KPIs)
                var (arqui, espe) = _familiaLogic.CalcularResumenGlobal(ventasCompletas);
                _cacheTarjetasGlobal = arqui.Concat(espe).ToList();

                _cacheVentaGlobal = _cacheTarjetasGlobal.Sum(x => x.VentaTotal);
                _cacheLitrosGlobal = _cacheTarjetasGlobal.Sum(x => x.LitrosTotal);

                GranTotalVenta = _cacheVentaGlobal;
                GranTotalLitros = _cacheLitrosGlobal;
                TituloReporteCard = "📥 REPORTE GLOBAL";

                TarjetasFamilias = new ObservableCollection<FamiliaResumenModel>(
                    _cacheTarjetasGlobal.OrderByDescending(x => x.LitrosTotal)
                );

                if (TarjetasFamilias.Count == 0)
                {
                    _notificationService.ShowInfo("El filtro de fechas dejó 0 ventas. ¡Revisa tu calendario!");
                }

                OnPropertyChanged(nameof(GranTotalVenta));
                OnPropertyChanged(nameof(GranTotalLitros));
            }
            catch (Exception ex)
            {
                await _notificationService.ShowErrorDialog($"Error cargando familias: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        private async void CargarDetalle(string familia)
        {
            IsLoading = true;
            try
            {
                TituloDetalle = familia;
                DateTime fechaSeguraInicio = Filters.FechaInicio != DateTime.MinValue
                    ? Filters.FechaInicio.Date
                    : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                DateTime fechaSeguraFin = Filters.FechaFin != DateTime.MinValue
                    ? Filters.FechaFin.Date
                    : DateTime.Now.Date;
                // 1. Descargamos TODAS las ventas del mes
                var ventasCompletas = await _reportesService.ObtenerVentasDetalleAsync(Filters.SucursalId, fechaSeguraInicio, fechaSeguraFin);

                if (Filters.FechaInicio != DateTime.MinValue)
                    ventasCompletas = ventasCompletas.Where(v => v.FechaEmision.Date >= Filters.FechaInicio.Date).ToList();

                if (Filters.FechaFin != DateTime.MinValue)
                    ventasCompletas = ventasCompletas.Where(v => v.FechaEmision.Date <= Filters.FechaFin.Date).ToList();

                // 3. Filtramos SOLO las que corresponden a la familia seleccionada
                _datosFamiliaActual = ventasCompletas.Where(x => x.Familia == familia).ToList();

                GranTotalVenta = _datosFamiliaActual.Sum(x => x.TotalVenta);
                GranTotalLitros = (double)_datosFamiliaActual.Sum(x => x.LitrosTotales);
                TituloReporteCard = "📥 DESCARGAR DETALLE";

                OnPropertyChanged(nameof(GranTotalVenta));
                OnPropertyChanged(nameof(GranTotalLitros));

                // 4. Llenamos el combo de sublíneas
                SubLineasDisponibles.Clear();
                SubLineasDisponibles.Add("TODAS");
                var lineas = _datosFamiliaActual.Select(x => x.Linea).Distinct().OrderBy(x => x).ToList();
                foreach (var l in lineas) SubLineasDisponibles.Add(l);

                SubLineaSeleccionada = "TODAS";
                OnPropertyChanged(nameof(TituloDetalle));

                VerResumen = false;

                _verPorLitros = true;
                OnPropertyChanged(nameof(VerPorLitros));
                ActualizarGraficosPorSubLinea();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Error al cargar detalle: " + ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
        private void CargarGraficosBarras(List<VentaReporteModel> listaOriginal)
        {
            bool isDark = false;
            try { isDark = Properties.Settings.Default.IsDarkMode; } catch { }
            var colorEtiquetasEje = isDark ? SKColors.White.WithAlpha(180) : SKColor.Parse("#757575");
            var colorNumerosBarra = isDark ? SKColors.White : SKColor.Parse("#333333");

            var resumen = listaOriginal
                .GroupBy(x => x.Descripcion)
                .Select(g => new
                {
                    Nombre = g.Key ?? "Sin Nombre",
                    TotalDinero = g.Sum(x => x.TotalVenta),
                    TotalLitros = g.Sum(x => x.LitrosTotales)
                })
                .OrderByDescending(x => x.TotalDinero)
                .Take(5)
                .OrderBy(x => x.TotalDinero)
                .ToList();

            var nombresDinero = resumen.Select(x => x.Nombre).ToArray();
            var valoresDinero = resumen.Select(x => x.TotalDinero).ToArray();
            SeriesBarrasDinero = new ObservableCollection<ISeries>
            {
                new RowSeries<decimal>
                {
                    Values = valoresDinero,
                    Name = "Venta Total",
                    Fill = new SolidColorPaint(SKColor.Parse("#1565C0")),
                    DataLabelsPaint = new SolidColorPaint(colorNumerosBarra),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsFormatter = point => FormatearNumeroFino((decimal)point.Model, true),
                    XToolTipLabelFormatter = point =>
                    {
                        var valor = (decimal)point.Model;
                        var indice = point.Index;

                        return $"{nombresDinero[indice]}: {FormatearNumeroFino(valor, true)}";
                    }
                }
            };
            EjeXValores = new Axis[]
            {
                new LogarithmicAxis(10)
                {
                    Labeler = value => FormatearNumeroFino((decimal)value, true),
                    LabelsPaint = new SolidColorPaint(colorEtiquetasEje)
                }
            };

            EjeYLineasDinero = new Axis[]
            {
                new Axis
                {
                    Labels = nombresDinero,
                    LabelsPaint = new SolidColorPaint(colorEtiquetasEje),
                    TextSize = 12
                }
            };
            var resumenLitros = resumen.OrderBy(x => x.TotalLitros).ToList();
            var nombresLitros = resumenLitros.Select(x => x.Nombre).ToArray();

            SeriesBarrasLitros = new ObservableCollection<ISeries>
            {
                new RowSeries<decimal>
                {
                    Values = resumenLitros.Select(x => x.TotalLitros).ToArray(),
                    Name = "Litros",
                    Fill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                    DataLabelsPaint = new SolidColorPaint(colorNumerosBarra),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsFormatter = point => FormatearNumeroFino((decimal)point.Model, false),
                    XToolTipLabelFormatter = point =>
                    {
                        var valor = (decimal)point.Model;
                        var indice = point.Index;

                        return $"{nombresLitros[indice]}: {FormatearNumeroFino(valor, false)}";
                    }
                }
            };

            EjeYLineasLitros = new Axis[]
            {
                new Axis
                {
                    Labels = resumenLitros.Select(x => x.Nombre).ToArray(),
                    LabelsPaint = new SolidColorPaint(colorEtiquetasEje),
                    TextSize = 12
                }
            };
        }
        private void ActualizarGraficosPorSubLinea()
        {
            if (_datosFamiliaActual == null) return;

            string filtro = SubLineaSeleccionada;
            bool esVistaGlobal = (filtro == "TODAS" || string.IsNullOrEmpty(filtro));

            var datosFiltrados = esVistaGlobal
                ? _datosFamiliaActual.ToList()
                : _datosFamiliaActual.Where(x => (x.Linea ?? "").Equals(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

            var resumenProductos = datosFiltrados
                .GroupBy(x => x.Descripcion)
                .Select(g => new SubLineaPerformanceModel
                {
                    Nombre = g.Key ?? "SIN DESCRIPCIÓN",
                    LitrosTotales = (decimal)g.Sum(x => x.Cantidad),
                    VentaTotal = g.Sum(x => x.TotalVenta)
                })
                .OrderByDescending(x => x.LitrosTotales)
                .ToList();

            ListaDesglose = new ObservableCollection<SubLineaPerformanceModel>(resumenProductos);
            OnPropertyChanged(nameof(ListaDesglose));

            var datosOrdenados = VerPorLitros
                ? datosFiltrados.OrderByDescending(x => x.Cantidad).ToList()
                : datosFiltrados.OrderByDescending(x => x.TotalVenta).ToList();

            DetalleVentas = new ObservableCollection<VentaReporteModel>(datosOrdenados);

            TituloGraficoPastel = esVistaGlobal ? "Distribución por Línea" : "Distribución por Producto";
            OnPropertyChanged(nameof(TituloGraficoPastel));

            CargarGraficosBarras(datosFiltrados);
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
        private string FormatearNumeroFino(decimal valor, bool esDinero)
        {
            string prefijo = esDinero ? "$" : "";
            string sufijo = esDinero ? "" : " L";

            if (valor >= 1000000)
                return $"{prefijo}{(valor / 1000000):0.##}M{sufijo}";
            if (valor >= 1000)
                return $"{prefijo}{(valor / 1000):0.#}K{sufijo}";

            return esDinero ? valor.ToString("C0") : $"{valor:N0} L";
        }
        private void GenerarGraficaEvolucion(List<VentaReporteModel> datos)
        {
            if (datos == null || !datos.Any()) return;

            var familiasOficiales = new List<string>
    {
        "Vinílica", "Esmaltes", "Impermeabilizantes", "Selladores",
        "Industrial", "Tráfico", "Solventes", "Accesorios"
    };

            // 1. Determinar el rango de tiempo para decidir cómo agrupar
            var fechaMin = datos.Min(x => x.FechaEmision.Date);
            var fechaMax = datos.Max(x => x.FechaEmision.Date);
            var diasDiferencia = (fechaMax - fechaMin).TotalDays;

            // Si el rango es de un mes o menos, agrupamos por día. Si es más, por mes.
            bool agruparPorDia = diasDiferencia <= 35;

            // 2. Crear los "puntos" en el tiempo (Días o Meses)
            List<DateTime> puntosTiempo;
            if (agruparPorDia)
            {
                puntosTiempo = datos.Select(x => x.FechaEmision.Date).Distinct().OrderBy(x => x).ToList();
                // Protección anti-colapso si solo hay 1 día
                if (puntosTiempo.Count == 1) puntosTiempo.Insert(0, puntosTiempo[0].AddDays(-1));
            }
            else
            {
                puntosTiempo = datos.Select(x => new DateTime(x.FechaEmision.Year, x.FechaEmision.Month, 1)).Distinct().OrderBy(x => x).ToList();
                // Protección anti-colapso si solo hay 1 mes
                if (puntosTiempo.Count == 1) puntosTiempo.Insert(0, puntosTiempo[0].AddMonths(-1));
            }

            // 3. Crear las etiquetas del Eje X con el formato adecuado
            var etiquetasEjeX = agruparPorDia
                ? puntosTiempo.Select(x => x.ToString("dd-MMM").ToUpper()).ToArray()
                : puntosTiempo.Select(x => x.ToString("MMM-yy").ToUpper()).ToArray();

            // 4. Limpiar y agrupar familias
            var datosAgrupadosLimpio = datos
                .GroupBy(x =>
                {
                    var nombre = x.Familia?.Trim() ?? "OTROS";
                    var oficial = familiasOficiales.FirstOrDefault(f => f.Equals(nombre, StringComparison.OrdinalIgnoreCase));
                    return oficial ?? "OTROS";
                })
                .OrderByDescending(g => g.Sum(x => x.TotalVenta));

            var coleccionSeries = new ObservableCollection<ISeries>();

            // 5. Llenar los puntos en la gráfica dinámicamente
            foreach (var familia in datosAgrupadosLimpio)
            {
                var ventasPorPunto = new List<decimal>();

                foreach (var punto in puntosTiempo)
                {
                    decimal sumaPunto = 0;
                    if (agruparPorDia)
                    {
                        // Busca ventas exactas de ese día
                        sumaPunto = familia.Where(x => x.FechaEmision.Date == punto.Date).Sum(x => x.TotalVenta);
                    }
                    else
                    {
                        // Busca ventas de todo ese mes
                        sumaPunto = familia.Where(x => x.FechaEmision.Year == punto.Year && x.FechaEmision.Month == punto.Month).Sum(x => x.TotalVenta);
                    }
                    ventasPorPunto.Add(sumaPunto);
                }

                coleccionSeries.Add(new LineSeries<decimal>
                {
                    Values = ventasPorPunto,
                    Name = familia.Key,
                    LineSmoothness = 0.65,
                    GeometrySize = 8,
                    YToolTipLabelFormatter = point =>
                    {
                        var valor = (decimal)point.Model;
                        return $" {point.Context.Series.Name} {FormatearNumeroFino(valor, true)} ";
                    }
                });
            }

            EjeXMeses = new Axis[]
            {
        new Axis
        {
            Labels = etiquetasEjeX,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            TextSize = 12
        }
            };

            SeriesEvolucionFamilias = coleccionSeries;
        }
    }
}