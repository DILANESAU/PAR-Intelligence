using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

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
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly SucursalesService _sucursalesService;
        private readonly CacheService _cacheService;
        public FilterService Filters { get; }

        private bool _isInitialized = false;

        // COLECCIONES
        public ObservableCollection<FamiliaResumenModel> TarjetasFamilias { get; set; } = new ObservableCollection<FamiliaResumenModel>();
        public ObservableCollection<FamiliaResumenModel> TarjetasArquitectonica { get; set; } = new ObservableCollection<FamiliaResumenModel>();
        public ObservableCollection<FamiliaResumenModel> TarjetasEspecializada { get; set; } = new ObservableCollection<FamiliaResumenModel>();
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

                    // Cuando cambia la sucursal, actualizamos el Filtro Global y recargamos
                    if ( value != null )
                    {
                        Filters.SucursalId = value.Id;

                        // Solo recargamos si ya estábamos inicializados para evitar doble carga al inicio
                        if ( _isInitialized )
                        {
                            EjecutarReporte();
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

        // Títulos
        public string TituloGraficoPastel { get; set; } = "Distribución";

        // --- BARRAS DOBLES (Clientes y Productos) ---
        public string TituloBarrasClientes { get; set; } = "Top Clientes";
        public string TituloBarrasProductos { get; set; } = "Top Productos";

        private decimal _cacheVentaGlobal;
        private double _cacheLitrosGlobal;
        private string _tituloReporteCard = "📥 REPORTE COMPLETO"; // Texto dinámico del botón

        public string TituloReporteCard
        {
            get => _tituloReporteCard;
            set { _tituloReporteCard = value; OnPropertyChanged(); }
        }
        public ISeries[] SeriesBarrasClientes { get; set; }
        public Axis[] EjeXBarrasClientes { get; set; }
        public Axis[] EjeYBarrasClientes { get; set; }

        public ISeries[] SeriesBarrasProductos { get; set; }
        public Axis[] EjeXBarrasProductos { get; set; }
        public Axis[] EjeYBarrasProductos { get; set; }

        // --- CONTROL TOP N ---
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
        // Propiedad vital para el ScrollViewer
        public double AlturaGraficaDinamica => Math.Max(450, TopSeleccionado * 50);

        // --- PASTELES ---
        public ISeries[] SeriesPastelDinero { get; set; }
        public ISeries[] SeriesPastelLitros { get; set; }

        public ISeries[] SeriesComportamientoLineas { get; set; }
        public Axis[] EjeXMensual { get; set; }
        public SolidColorPaint LegendTextPaint { get; set; }

        // ESTADO
        private List<VentaReporteModel> _ventasProcesadas;
        private List<VentaReporteModel> _datosAnualesCache;
        private List<VentaReporteModel> _datosFamiliaActual;
        private string _lineaActual = "Todas";

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
        public bool ExcluirBlancos
        {
            get => _excluirBlancos;
            set
            {
                _excluirBlancos = value;
                OnPropertyChanged();
                ActualizarGraficosPorSubLinea(); // <--- Recalcula al cambiar el switch
            }
        }

        // Usamos la lista del servicio FilterService, pero si necesitas exponerla localmente:
        // public ObservableCollection<KeyValuePair<int, string>> ListaSucursales => Filters.ListaSucursales; 

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

        public FamiliaViewModel(
             IDialogService dialogService,
             BusinessLogicService businessLogic,
            FilterService filterService,
            ChartService chartService,
            FamiliaLogicService familiaLogic,
            INotificationService notificationService,
            SucursalesService sucursalesService, CacheService cacheService)
        {
            _dialogService = dialogService;
            _cacheService = cacheService;
            Filters = filterService;
            _chartService = chartService;
            _familiaLogic = familiaLogic;
            _notificationService = notificationService;
            _sucursalesService = sucursalesService;
            _reportesService = new ReportesService();
            _catalogoService = new CatalogoService(businessLogic);

            _ventasProcesadas = new List<VentaReporteModel>();
            _datosAnualesCache = new List<VentaReporteModel>();

            // MODIFICACIÓN: Ya no llamamos a CargarListaSucursales aquí 
            // para evitar conflictos con la inicialización de la View.

            ActualizarColoresGraficos();

            // Comandos
            ActualizarCommand = new RelayCommand(o => EjecutarReporte());
            OrdenarVentaCommand = new RelayCommand(o => AplicarOrden("VENTA"));
            OrdenarNombreCommand = new RelayCommand(o => AplicarOrden("NOMBRE"));
            RegresarCommand = new RelayCommand(o => RestaurarVistaGeneral());
            ExportarGlobalCommand = new RelayCommand(o => {
                if ( VerResumen ) GenerarReporteExcel(true);
                else GenerarReporteExcel(false);
            });
            VerDetalleCommand = new RelayCommand(param => { if ( param is string familia ) CargarDetalle(familia); });
            CambiarPeriodoGraficoCommand = new RelayCommand(param => { if ( param is string periodo ) GenerarDesglosePorPeriodo(periodo); });

            Filters.OnFiltrosCambiados += EjecutarReporte;
        }
        private void RestaurarVistaGeneral()
        {
            VerResumen = true;

            // RESTAURAR LOS VALORES GLOBALES DESDE LA CACHÉ
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
            // 1. Cargamos sucursales solo si la lista está vacía
            if ( Sucursales.Count == 0 )
            {
                try
                {
                    Sucursales.Clear();
                    // Opción "TODAS" siempre presente
                    Sucursales.Add(new SucursalModel { Id = 0, Nombre = "0 - TODAS" });

                    // Obtenemos los datos (ahora incrustados en el servicio)
                    var dic = _sucursalesService.CargarSucursales();
                    if ( dic != null )
                    {
                        foreach ( var kvp in dic )
                            Sucursales.Add(new SucursalModel { Id = kvp.Key, Nombre = $"{kvp.Key} - {kvp.Value}" });
                    }

                    // Seleccionamos la sucursal por defecto guardada en Configuración
                    int idGuardado = Properties.Settings.Default.SucursalDefaultId;
                    var encontrada = Sucursales.FirstOrDefault(s => s.Id == idGuardado);

                    // Esto dispara el setter y actualiza el Filtro Global
                    SucursalSeleccionada = encontrada ?? Sucursales.First();
                }
                catch ( Exception ex )
                {
                    _notificationService.ShowError("Error cargando sucursales: " + ex.Message);
                }
            }

            // 2. Si es la primera vez que entramos a la pestaña, ejecutamos el reporte
            if ( !_isInitialized )
            {
                _notificationService.ShowInfo("Cargando datos de familias...");
                CargarPorLinea("Todas");
                EjecutarReporte();
                _isInitialized = true;
            }
        }
        
        public void CargarPorLinea(string linea)
        {
            _lineaActual = linea;
            if ( _ventasProcesadas != null && _ventasProcesadas.Any() )
            {
                GenerarResumenVisual();
                _notificationService.ShowSuccess($"Visualizando línea: {linea.ToUpper()}");
            }
            else
            {
                TarjetasFamilias = new ObservableCollection<FamiliaResumenModel>(_familiaLogic.ObtenerTarjetasVacias(_lineaActual));
            }
            VerResumen = true;
        }

        public void DetenerRenderizado()
        {
            // Poner las series en null o en array vacío detiene los cálculos de dibujo
            SeriesPastelDinero = Array.Empty<ISeries>();
            SeriesPastelLitros = Array.Empty<ISeries>();
            //SeriesBarrasTop = Array.Empty<ISeries>();
            SeriesBarrasClientes = Array.Empty<ISeries>();
            SeriesBarrasProductos = Array.Empty<ISeries>();

            // Notificar a la vista para que se actualice antes de morir
            OnPropertyChanged(nameof(SeriesPastelDinero));
            OnPropertyChanged(nameof(SeriesPastelLitros));
            OnPropertyChanged(nameof(SeriesBarrasProductos));
            OnPropertyChanged(nameof(SeriesBarrasClientes));
            //OnPropertyChanged(nameof(SeriesBarrasTop));
        }
        private async void EjecutarReporte()
        {
            IsLoading = true;
            try
            {
                var ventasRaw = await _reportesService.ObtenerVentasBrutasRango(Filters.SucursalId, Filters.FechaInicio, Filters.FechaFin);
                _ventasProcesadas = ventasRaw;

                foreach ( var venta in _ventasProcesadas )
                {
                    var info = _catalogoService.ObtenerInfo(venta.Articulo);
                    venta.Familia = info.FamiliaSimple;
                    venta.Linea = info.Linea;
                    venta.Descripcion = info.Descripcion;
                    venta.LitrosUnitarios = info.Litros;
                }

                _ventasProcesadas = _ventasProcesadas.Where(x => x.Familia != "FERRETERIA" && !x.Linea.Contains("FERRETERIA", StringComparison.OrdinalIgnoreCase)).ToList();

                GenerarResumenVisual();
                IsLoading = false;

                if ( _ventasProcesadas.Count == 0 ) _notificationService.ShowInfo("No hay resultados.");

                _datosAnualesCache = await _reportesService.ObtenerHistoricoAnualPorArticulo(DateTime.Now.Year.ToString(), Filters.SucursalId.ToString());
                foreach ( var item in _datosAnualesCache )
                {
                    var info = _catalogoService.ObtenerInfo(item.Articulo);
                    item.Linea = info.Linea;
                    item.Familia = info.FamiliaSimple;
                }

                if ( VerDetalle ) GenerarDesglosePorPeriodo("ANUAL");
            }
            catch ( Exception ex )
            {
                IsLoading = false;
                await _notificationService.ShowErrorDialog($"Error: {ex.Message}");
            }
        }

        private void GenerarResumenVisual()
        {
            var (arqui, espe) = _familiaLogic.CalcularResumenGlobal(_ventasProcesadas);

            // 1. Calculamos los globales REALES
            _cacheVentaGlobal = _ventasProcesadas.Sum(x => x.TotalVenta);
            _cacheLitrosGlobal = _ventasProcesadas.Sum(x => x.LitrosTotales);

            // 2. Los asignamos a la vista
            GranTotalVenta = _cacheVentaGlobal;
            GranTotalLitros = _cacheLitrosGlobal;
            TituloReporteCard = "📥 REPORTE GLOBAL"; // Texto por defecto

            // ... resto del código (asignar TarjetasFamilias, etc.)
            IEnumerable<FamiliaResumenModel> resultado;
            if ( _lineaActual == "Arquitectonica" ) resultado = arqui;
            else if ( _lineaActual == "Especializada" ) resultado = espe;
            else resultado = arqui.Concat(espe);

            TarjetasFamilias = new ObservableCollection<FamiliaResumenModel>(resultado);

            // Notificar cambios
            OnPropertyChanged(nameof(GranTotalVenta));
            OnPropertyChanged(nameof(GranTotalLitros));
            OnPropertyChanged(nameof(TarjetasFamilias));
        }

        private void CargarDetalle(string familia)
        {
            TituloDetalle = familia;
            _datosFamiliaActual = _ventasProcesadas.Where(x => x.Familia == familia).ToList();

            // --- CAMBIO CLAVE: Actualizar los KPIs superiores con datos de LA FAMILIA ---
            GranTotalVenta = _datosFamiliaActual.Sum(x => x.TotalVenta);
            GranTotalLitros = _datosFamiliaActual.Sum(x => x.LitrosTotales);
            TituloReporteCard = "📥 DESCARGAR DETALLE"; // Cambiamos el texto del botón

            // Notificar a la vista para que los números cambien
            OnPropertyChanged(nameof(GranTotalVenta));
            OnPropertyChanged(nameof(GranTotalLitros));
            // --------------------------------------------------------------------------

            SubLineasDisponibles.Clear();
            SubLineasDisponibles.Add("TODAS");
            var lineas = _datosFamiliaActual.Select(x => x.Linea).Distinct().OrderBy(x => x).ToList();
            foreach ( var l in lineas ) SubLineasDisponibles.Add(l);

            SubLineaSeleccionada = "TODAS";
            OnPropertyChanged(nameof(TituloDetalle));

            GenerarDesglosePorPeriodo("ANUAL");
            VerResumen = false;
        }

        private void ActualizarGraficosPorSubLinea()
        {
            if ( _datosFamiliaActual == null ) return;

            // 1. Filtrado Base (Quitamos Ferretería si aplica)
            var datosBase = _datosFamiliaActual
                .Where(x => !x.Linea.Contains("FERRETERIA", StringComparison.OrdinalIgnoreCase))
                .ToList();

            string filtro = SubLineaSeleccionada;
            bool esVistaGlobal = ( filtro == "TODAS" || string.IsNullOrEmpty(filtro) );

            var datosFiltrados = esVistaGlobal
                ? datosBase.ToList()
                : datosBase.Where(x => ( x.Linea ?? "" ).Equals(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

            DetalleVentas = new ObservableCollection<VentaReporteModel>(datosFiltrados.OrderByDescending(x => x.TotalVenta));

            // Títulos
            TituloGraficoPastel = esVistaGlobal ? "Distribución por Línea ($)" : "Distribución por Producto ($)";

            // Datos para Pasteles (Agrupación)
            var resumenDatos = esVistaGlobal
                ? datosFiltrados
                    .GroupBy(x => x.Linea)
                    .Select(g => new { Nombre = g.Key, Venta = g.Sum(x => x.TotalVenta), Litros = ( double ) g.Sum(x => x.LitrosTotales) })
                    .ToList()
                : datosFiltrados
                    .GroupBy(x => x.Descripcion)
                    .Select(g => new { Nombre = g.Key, Venta = g.Sum(x => x.TotalVenta), Litros = ( double ) g.Sum(x => x.LitrosTotales) })
                    .ToList();

            // ========================================================================
            // 1. GRÁFICOS DE PASTEL (Estilo Sólido - InnerRadius 0)
            // ========================================================================

            // Pastel Dinero
            SeriesPastelDinero = resumenDatos
                .OrderByDescending(x => x.Venta)
                .Take(5)
                .Select(x => new PieSeries<double>
                {
                    Values = new double[] { ( double ) x.Venta },
                    Name = NormalizarNombreProducto(x.Nombre),
                    InnerRadius = 0, // CERO para pastel completo

                    // Formato limpio: "$1,500 (45%)"
                    DataLabelsFormatter = p => $"{p.Model:C0} ({p.StackedValue.Share:P0})",

                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 11,
                    ToolTipLabelFormatter = p => $"{p.Context.Series.Name}: {p.Model:C0} ({p.StackedValue.Share:P1})"
                }).ToArray();

            // Pastel Litros
            SeriesPastelLitros = resumenDatos
                .OrderByDescending(x => x.Litros)
                .Take(5)
                .Select(x => new PieSeries<double>
                {
                    Values = new double[] { x.Litros },
                    Name = NormalizarNombreProducto(x.Nombre),
                    InnerRadius = 0, // CERO para pastel completo

                    // Formato limpio: "500 L (20%)"
                    DataLabelsFormatter = p => $"{p.Model:N0} L ({p.StackedValue.Share:P0})",

                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 11,
                    ToolTipLabelFormatter = p => $"{p.Context.Series.Name}: {p.Model:N0} L ({p.StackedValue.Share:P1})"
                }).ToArray();


            // ========================================================================
            // 2. GRÁFICOS DE BARRAS (Top Clientes y Productos)
            // ========================================================================

            // --- IZQUIERDA: TOP CLIENTES ---
            // (A los clientes NO les aplicamos el filtro de blancos, queremos ver quién compra más en general)
            var datosParaClientes = datosFiltrados.Select(x => new VentaReporteModel
            {
                Descripcion = x.Cliente, // TRUCO: Pasamos Cliente en la Descripción para que el servicio agrupe por Cliente
                TotalVenta = x.TotalVenta,
                Cantidad = x.LitrosTotales,
                LitrosUnitarios = 1
            }).ToList();

            var resClientes = _chartService.GenerarTopProductos(datosParaClientes, VerPorLitros, TopSeleccionado);

            SeriesBarrasClientes = resClientes.Series;
            EjeXBarrasClientes = resClientes.EjesX;
            EjeYBarrasClientes = resClientes.EjesY;
            TituloBarrasClientes = $"Top {TopSeleccionado} Clientes";


            // --- DERECHA: TOP PRODUCTOS (Con lógica de Excluir Blancos) ---
            var datosParaProductos = datosFiltrados.ToList();

            if ( ExcluirBlancos )
            {
                // Filtramos cualquier producto que parezca BLANCO
                datosParaProductos = datosParaProductos
                    .Where(x => !x.Descripcion.ToUpper().Contains("BLANCO")
                             && !x.Descripcion.ToUpper().Contains(" BCO ")
                             && !x.Descripcion.ToUpper().EndsWith(" BCO"))
                    .ToList();

                TituloBarrasProductos = $"Top {TopSeleccionado} Colores (Sin Blancos)";
            }
            else
            {
                TituloBarrasProductos = $"Top {TopSeleccionado} Productos";
            }

            var resProductos = _chartService.GenerarTopProductos(datosParaProductos, VerPorLitros, TopSeleccionado);

            SeriesBarrasProductos = resProductos.Series;
            EjeXBarrasProductos = resProductos.EjesX;
            EjeYBarrasProductos = resProductos.EjesY;

            // ========================================================================
            // 3. NOTIFICACIONES A LA VISTA
            // ========================================================================
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
        private string Truncar(string t, int m) => t.Length > m ? t.Substring(0, m - 3) + "..." : t;
        private string NormalizarNombreProducto(string n) { if ( string.IsNullOrEmpty(n) ) return ""; string l = n.Trim(); if ( l.Contains("-") ) { var p = l.Split('-'); if ( p.Length > 1 ) l = p[1]; } return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(l.ToLower()); }

        private void AplicarOrden(string criterio) { if ( TarjetasFamilias != null ) { TarjetasFamilias = new ObservableCollection<FamiliaResumenModel>(_familiaLogic.OrdenarTarjetas(TarjetasFamilias.ToList(), criterio)); OnPropertyChanged(nameof(TarjetasFamilias)); } }
        private void GenerarDesglosePorPeriodo(string periodo) { if ( _datosAnualesCache != null && _datosAnualesCache.Any() ) { var datos = _datosAnualesCache.Where(x => x.Familia == TituloDetalle).ToList(); var g = _chartService.GenerarTendenciaLineas(datos, periodo); SeriesComportamientoLineas = g.Series; EjeXMensual = g.EjesX; OnPropertyChanged(nameof(SeriesComportamientoLineas)); OnPropertyChanged(nameof(EjeXMensual)); var l = _familiaLogic.CalcularDesgloseClientes(datos, periodo == "ANUAL" ? "TRIMESTRAL" : periodo); ListaDesglose = new ObservableCollection<SubLineaPerformanceModel>(l); OnPropertyChanged(nameof(ListaDesglose)); } }
        // En FamiliaViewModel.cs

        private async void GenerarReporteExcel(bool esGlobal)
        {
            var datosParaExportar = esGlobal ? _ventasProcesadas : DetalleVentas?.ToList();

            if ( datosParaExportar == null || !datosParaExportar.Any() )
            {
                _notificationService.ShowInfo("No hay datos para exportar.");
                return;
            }

            // Cambiamos la extensión a .xlsx
            string path = _dialogService.ShowSaveFileDialog("Excel|*.xlsx", $"Reporte_{DateTime.Now:yyyyMMdd}.xlsx");

            if ( !string.IsNullOrEmpty(path) )
            {
                IsLoading = true;
                await Task.Run(() =>
                {
                    // Usamos el nuevo servicio
                    var exporter = new ExportService();
                    exporter.ExportarExcelVentas(datosParaExportar, path);
                });

                IsLoading = false;
            }
        }
        private void FiltrarTabla() { if ( DetalleVentas != null ) { var v = CollectionViewSource.GetDefaultView(DetalleVentas); if ( string.IsNullOrWhiteSpace(TextoBusqueda) ) v.Filter = null; else { string t = TextoBusqueda.ToUpper(); v.Filter = o => { if ( o is VentaReporteModel m ) return ( m.Cliente?.ToUpper().Contains(t) ?? false ) || ( m.Descripcion?.ToUpper().Contains(t) ?? false ); return false; }; } } }
    }
}