using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;

using SkiaSharp;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

// 1. Apuntamos al Core para Modelos y Servicios de Datos
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;

// 2. Apuntamos a los servicios propios de la UI (Dialogos, Notificaciones)
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;
using WPF_PAR.Converters; // Si usas ObservableObject desde aquí

namespace WPF_PAR.MVVM.ViewModels
{
    public class ClientesViewModel : ObservableObject
    {
        // --- SERVICIOS ---
        private readonly ReportesService _reportesService;
        private readonly ClientesLogicService _logicService;
        private readonly CatalogoService _catalogoService;
        private readonly SucursalesService _sucursalesService;

        // Servicios de UI (Interfaces)
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;

        public FilterService Filters { get; }

        private bool _isInitialized = false;

        // --- FILTROS ---
        public ObservableCollection<int> AñosDisponibles { get; set; }
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
                            CargarDatosIniciales();
                            _notificationService.ShowInfo($"Analizando clientes de: {value.Nombre}");
                        }
                    }
                }
            }
        }

        private int _anioSeleccionado;
        public int AnioSeleccionado
        {
            get => _anioSeleccionado;
            set
            {
                if ( _anioSeleccionado != value )
                {
                    _anioSeleccionado = value;
                    OnPropertyChanged();
                    CargarDatosIniciales();
                }
            }
        }

        private string _modoSeleccionado = "Anual";
        public ObservableCollection<string> ModosVista { get; } = new ObservableCollection<string> { "Anual", "Semestral", "Trimestral" };
        public string ModoSeleccionado
        {
            get => _modoSeleccionado;
            set
            {
                _modoSeleccionado = value;
                OnPropertyChanged();
                CalcularVisibilidadPeriodos();
                ActualizarGrafica();

                if ( ClienteSeleccionado != null )
                {
                    _ = CargarProductosDinamicos();
                }
            }
        }

        // --- DATOS PRINCIPALES ---
        private List<ClienteAnalisisModel> _todosLosClientes;
        private ObservableCollection<ClienteAnalisisModel> _clientesResumen;
        public ObservableCollection<ClienteAnalisisModel> ClientesResumen
        {
            get => _clientesResumen;
            set { _clientesResumen = value; OnPropertyChanged(); }
        }

        // --- CLIENTE SELECCIONADO ---
        private ClienteAnalisisModel _clienteSeleccionado;
        public ClienteAnalisisModel ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                _clienteSeleccionado = value;
                OnPropertyChanged();
                if ( value != null )
                {
                    CargarDetalleAdicional(value);
                    ActualizarGrafica();
                }
            }
        }

        private KpiClienteModel _kpisDetalle;
        public KpiClienteModel KpisDetalle
        {
            get => _kpisDetalle;
            set { _kpisDetalle = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ProductoAnalisisModel> _productosEnDeclive;
        public ObservableCollection<ProductoAnalisisModel> ProductosEnDeclive
        {
            get => _productosEnDeclive;
            set { _productosEnDeclive = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ProductoAnalisisModel> _productosEnAumento;
        public ObservableCollection<ProductoAnalisisModel> ProductosEnAumento
        {
            get => _productosEnAumento;
            set { _productosEnAumento = value; OnPropertyChanged(); }
        }

        // --- GRÁFICAS ---
        private ISeries[] _seriesGrafica;
        public ISeries[] SeriesGrafica { get => _seriesGrafica; set { _seriesGrafica = value; OnPropertyChanged(); } }
        public Axis[] EjeXGrafica { get; set; }
        public Axis[] EjeYGrafica { get; set; }

        // --- NAVEGACIÓN Y ESTADO ---
        private bool _enModoDetalle;
        public bool EnModoDetalle
        {
            get => _enModoDetalle;
            set
            {
                _enModoDetalle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EnModoLista));
            }
        }
        public bool EnModoLista => !EnModoDetalle;

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private string _textoBusqueda;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set { _textoBusqueda = value; OnPropertyChanged(); FiltrarTabla(); }
        }

        // --- KPIs GLOBALES ---
        private int _totalClientesActivos;
        public int TotalClientesActivos { get => _totalClientesActivos; set { _totalClientesActivos = value; OnPropertyChanged(); } }

        private int _totalClientesInactivos;
        public int TotalClientesInactivos { get => _totalClientesInactivos; set { _totalClientesInactivos = value; OnPropertyChanged(); } }

        private readonly ClientesService _clientesService;

        // --- VISIBILIDAD ---
        public Visibility VisibilityQ1 { get; set; } = Visibility.Collapsed;
        public Visibility VisibilityQ2 { get; set; } = Visibility.Collapsed;
        public Visibility VisibilityQ3 { get; set; } = Visibility.Collapsed;
        public Visibility VisibilityQ4 { get; set; } = Visibility.Collapsed;
        public Visibility VisibilityS1 { get; set; } = Visibility.Collapsed;
        public Visibility VisibilityS2 { get; set; } = Visibility.Collapsed;

        // --- COMANDOS ---
        public RelayCommand ActualizarCommand { get; set; }
        public RelayCommand VerDetalleCommand { get; set; }
        public RelayCommand VolverListaCommand { get; set; }
        public RelayCommand ImprimirReporteCommand { get; set; }

        // =============================================================================
        // CONSTRUCTOR ACTUALIZADO (Recibe string, crea servicios)
        // =============================================================================
        public ClientesViewModel(ReportesService reportesService, ClientesService clientesService, SucursalesService sucursalesService, ClientesLogicService logicService, CatalogoService catalogoService, IDialogService dialogService, INotificationService notificationService, FilterService filterService)
        {
            _reportesService = reportesService;
            _clientesService = clientesService;
            _sucursalesService = sucursalesService;
            _logicService = logicService;
            _catalogoService = catalogoService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            Filters = filterService;

            // 4. Configuración inicial
            int year = DateTime.Now.Year;
            AñosDisponibles = new ObservableCollection<int> { year, year - 1, year - 2, year - 3 };
            _anioSeleccionado = year;

            // 5. Configurar Comandos
            ActualizarCommand = new RelayCommand(o => CargarDatosIniciales());
            ImprimirReporteCommand = new RelayCommand(o => GenerarPdfCliente());

            VerDetalleCommand = new RelayCommand(param => {
                if ( param is ClienteAnalisisModel cliente )
                {
                    ClienteSeleccionado = cliente;
                    EnModoDetalle = true;
                }
            });

            VolverListaCommand = new RelayCommand(o => {
                DetenerRenderizado();
                ClienteSeleccionado = null;
                KpisDetalle = null;
                EnModoDetalle = false;
            });

            Filters.OnFiltrosCambiados += CargarDatosIniciales;
        }

        // =============================================================================
        // MÉTODOS (Ligeramente ajustados para tareas asíncronas)
        // =============================================================================

        public async void CargarDatosIniciales()
        {
            if ( Sucursales.Count == 0 )
            {
                Sucursales.Clear();
                Sucursales.Add(new SucursalModel { Id = 0, Nombre = "0 - TODAS" });

                // NOTA: Verifica que tu SucursalesService devuelva el diccionario correctamente
                var dic = await Task.Run(() => _sucursalesService.CargarSucursales());

                if ( dic != null )
                {
                    foreach ( var kvp in dic )
                        Sucursales.Add(new SucursalModel { Id = kvp.Key, Nombre = $"{kvp.Key} - {kvp.Value}" });
                }

                int idGuardado = Properties.Settings.Default.SucursalDefaultId;
                var encontrada = Sucursales.FirstOrDefault(s => s.Id == idGuardado);
                SucursalSeleccionada = encontrada ?? Sucursales.First();
            }

            IsLoading = true;
            try
            {
                _todosLosClientes = await _clientesService.ObtenerDatosBase(AnioSeleccionado, Filters.SucursalId);

                TotalClientesActivos = _todosLosClientes.Count(x => x.VentasMensualesActual.Sum() > 0);
                TotalClientesInactivos = _todosLosClientes.Count(x => x.VentasMensualesActual.Sum() == 0 && x.VentasMensualesAnterior.Sum() > 0);

                FiltrarTabla();
                CalcularVisibilidadPeriodos();
                ClienteSeleccionado = null;
                SeriesGrafica = null;
                _isInitialized = true;
            }
            catch ( Exception ex )
            {
                _notificationService.ShowError("Error al cargar clientes: " + ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CargarProductosDinamicos()
        {
            if ( ClienteSeleccionado == null ) return;
            try
            {
                // El Worker ya hizo la magia de las fechas, solo le pedimos el año
                var todosProductos = await _clientesService.ObtenerVariacionProductos(
                    ClienteSeleccionado.Nombre,
                    AnioSeleccionado,
                    Filters.SucursalId);

                // Agrupamos el top 10 (El worker ya podría traer solo el Top 10 si así lo configuraste)
                var declive = todosProductos.Where(x => ( x.VentaActual - x.VentaAnterior ) < 0).OrderBy(x => ( x.VentaActual - x.VentaAnterior )).Take(10).ToList();
                var aumento = todosProductos.Where(x => ( x.VentaActual - x.VentaAnterior ) > 0).OrderByDescending(x => ( x.VentaActual - x.VentaAnterior )).Take(10).ToList();

                ProductosEnDeclive = new ObservableCollection<ProductoAnalisisModel>(declive);
                ProductosEnAumento = new ObservableCollection<ProductoAnalisisModel>(aumento);

                OnPropertyChanged(nameof(ProductosEnDeclive));
                OnPropertyChanged(nameof(ProductosEnAumento));
            }
            catch ( Exception ex ) { Debug.WriteLine("Error cargando productos: " + ex.Message); }
        }

        private async void GenerarPdfCliente()
        {
            if ( ClienteSeleccionado == null ) return;

            string path = _dialogService.ShowSaveFileDialog("PDF Document|*.pdf", $"Reporte_{ClienteSeleccionado.Nombre}.pdf");

            if ( !string.IsNullOrEmpty(path) )
            {
                IsLoading = true;
                var listAumento = ProductosEnAumento?.ToList() ?? new List<ProductoAnalisisModel>();
                var listDeclive = ProductosEnDeclive?.ToList() ?? new List<ProductoAnalisisModel>();

                // Capturamos valores locales para usar en el Task
                int sucId = Filters.SucursalId;
                string nombreCliente = ClienteSeleccionado.Nombre;
                var kpis = KpisDetalle;

                await Task.Run(async () =>
                {
                    var fin = DateTime.Now;
                    var inicio = fin.AddYears(-1);
                    var ventasRaw = await _reportesService.ObtenerVentasRangoAsync(sucId, inicio, fin);

                    var movimientos = ventasRaw
                        .Where(x => x.Cliente == nombreCliente)
                        .OrderByDescending(x => x.FechaEmision)
                        .Take(100)
                        .ToList();

                    foreach ( var venta in movimientos )
                    {
                        var info = _catalogoService.ObtenerInfo(venta.Articulo);
                        venta.Descripcion = info.Descripcion;
                        venta.LitrosUnitarios = ( double ) info.Litros;
                        // Si tu lógica requiere calcular totales manuales:
                        // venta.LitrosTotal = venta.Cantidad * (double)info.Litros;
                    }

                    var exporter = new ExportService();
                    exporter.ExportarPdfCliente(
                        _clienteSeleccionado, 
                        kpis,
                        movimientos,
                        listAumento,
                        listDeclive,
                        path
                    );
                });

                IsLoading = false;
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
            }
        }

        private (DateTime Inicio, DateTime Fin) ObtenerRangoFechas()
        {
            int anio = AnioSeleccionado;
            int mes = DateTime.Now.Month;

            if ( ModoSeleccionado == "Anual" )
                return (new DateTime(anio, 1, 1), new DateTime(anio, 12, 31));

            if ( ModoSeleccionado == "Semestral" )
            {
                bool esS2 = mes > 6;
                return esS2
                    ? (new DateTime(anio, 7, 1), new DateTime(anio, 12, 31))
                    : (new DateTime(anio, 1, 1), new DateTime(anio, 6, 30));
            }

            if ( ModoSeleccionado == "Trimestral" )
            {
                if ( mes <= 3 ) return (new DateTime(anio, 1, 1), new DateTime(anio, 3, 31));
                if ( mes <= 6 ) return (new DateTime(anio, 4, 1), new DateTime(anio, 6, 30));
                if ( mes <= 9 ) return (new DateTime(anio, 7, 1), new DateTime(anio, 9, 30));
                return (new DateTime(anio, 10, 1), new DateTime(anio, 12, 31));
            }

            return (new DateTime(anio, 1, 1), new DateTime(anio, 12, 31));
        }

        private async void CargarDetalleAdicional(ClienteAnalisisModel cliente) // Ojo al cambio de tipo si aplica
        {
            if ( cliente == null ) return;
            IsLoading = true;

            try
            {
                // Usamos el servicio de clientes y le pasamos el nombre
                KpisDetalle = await _clientesService.ObtenerKpisCliente(cliente.Nombre, AnioSeleccionado, Filters.SucursalId);
                await CargarProductosDinamicos();
            }
            catch ( Exception ex )
            {
                _dialogService.ShowMessage("Error", ex.Message);
            }
            finally { IsLoading = false; }
        }

        // =============================================================================
        // LÓGICA VISUAL
        // =============================================================================
        private void CalcularVisibilidadPeriodos()
        {
            bool esTri = ModoSeleccionado == "Trimestral";
            bool esSem = ModoSeleccionado == "Semestral";

            VisibilityQ1 = esTri ? Visibility.Visible : Visibility.Collapsed;
            VisibilityQ2 = esTri ? Visibility.Visible : Visibility.Collapsed;
            VisibilityQ3 = esTri ? Visibility.Visible : Visibility.Collapsed;
            VisibilityQ4 = esTri ? Visibility.Visible : Visibility.Collapsed;
            VisibilityS1 = esSem ? Visibility.Visible : Visibility.Collapsed;
            VisibilityS2 = esSem ? Visibility.Visible : Visibility.Collapsed;

            OnPropertyChanged(nameof(VisibilityQ1)); OnPropertyChanged(nameof(VisibilityQ2));
            OnPropertyChanged(nameof(VisibilityQ3)); OnPropertyChanged(nameof(VisibilityQ4));
            OnPropertyChanged(nameof(VisibilityS1)); OnPropertyChanged(nameof(VisibilityS2));
        }

        private void ActualizarGrafica()
        {
            if ( ClienteSeleccionado == null || ClienteSeleccionado.VentasMensualesActual == null )
            {
                SeriesGrafica = null;
                return;
            }

            var historia = ClienteSeleccionado.VentasMensualesActual;
            var valores = new List<decimal>();
            string[] etiquetas = null;

            switch ( ModoSeleccionado )
            {
                case "Anual":
                    valores = historia.ToList();
                    etiquetas = new[] { "ENE", "FEB", "MAR", "ABR", "MAY", "JUN", "JUL", "AGO", "SEP", "OCT", "NOV", "DIC" };
                    break;
                case "Semestral":
                    valores.Add(historia.Take(6).Sum());
                    valores.Add(historia.Skip(6).Take(6).Sum());
                    etiquetas = new[] { "SEM 1", "SEM 2" };
                    break;
                case "Trimestral":
                    for ( int i = 0; i < 4; i++ ) valores.Add(historia.Skip(i * 3).Take(3).Sum());
                    etiquetas = new[] { "T1", "T2", "T3", "T4" };
                    break;
            }

            SeriesGrafica = new ISeries[]
            {
                new LineSeries<decimal>
                {
                    Values = valores,
                    Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(30)),
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 },
                    GeometrySize = 8,
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = p => p.Model >= 1000000 ? $"{p.Model/1000000:N1}M" : (p.Model >= 1000 ? $"{p.Model/1000:N0}K" : $"{p.Model:N0}")
                }
            };

            EjeXGrafica = new Axis[] { new Axis { Labels = etiquetas, LabelsRotation = 0, TextSize = 12 } };
            EjeYGrafica = new Axis[] { new Axis { Labeler = v => v >= 1000 ? $"{v / 1000:N0}K" : $"{v:N0}", ShowSeparatorLines = true } };

            OnPropertyChanged(nameof(EjeXGrafica));
            OnPropertyChanged(nameof(EjeYGrafica));
        }

        public void DetenerRenderizado()
        {
            SeriesGrafica = null;
        }

        private void FiltrarTabla()
        {
            if ( _todosLosClientes == null ) return;

            if ( string.IsNullOrWhiteSpace(TextoBusqueda) )
            {
                ClientesResumen = new ObservableCollection<ClienteAnalisisModel>(_todosLosClientes);
            }
            else
            {
                var filtrados = _todosLosClientes
                    .Where(x => x.Nombre.IndexOf(TextoBusqueda, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                ClientesResumen = new ObservableCollection<ClienteAnalisisModel>(filtrados);
            }
        }
    }
}