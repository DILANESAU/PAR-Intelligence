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
using System.Windows.Media;

using WPF_PAR.Core;
using WPF_PAR.MVVM.Models;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class ClientesViewModel : ObservableObject
    {
        // --- SERVICIOS ---
        private readonly ReportesService _reportesService;
        private readonly ClientesLogicService _logicService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly CatalogoService _catalogoService;
        private readonly SucursalesService _sucursalesService;
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

                    // Cuando cambia la sucursal, actualizamos el Filtro Global y recargamos
                    if ( value != null )
                    {
                        Filters.SucursalId = value.Id;

                        // Solo recargamos si ya estábamos inicializados
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
                    CargarDatosIniciales(); // Recargar al cambiar año
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

                // ¡NUEVO! Recargar productos al cambiar modo
                if ( ClienteSeleccionado != null )
                {
                    _ = CargarProductosDinamicos(); // Llamada asíncrona fire-and-forget segura en setter
                }
            }
        }

        // --- DATOS PRINCIPALES ---
        private List<ClienteResumenModel> _todosLosClientes; // Copia maestra para búsqueda local
        private ObservableCollection<ClienteResumenModel> _clientesResumen;
        public ObservableCollection<ClienteResumenModel> ClientesResumen
        {
            get => _clientesResumen;
            set { _clientesResumen = value; OnPropertyChanged(); }
        }

        // --- CLIENTE SELECCIONADO (DETALLE) ---
        private ClienteResumenModel _clienteSeleccionado;
        public ClienteResumenModel ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                _clienteSeleccionado = value;
                OnPropertyChanged();
                if ( value != null )
                {
                    CargarDetalleAdicional(value); // Cargar KPIs SQL
                    ActualizarGrafica();           // Dibujar gráfica
                }
            }
        }

        // --- DATOS DEL DETALLE ---
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

        // --- KPIs GLOBALES (Cabecera) ---
        private int _totalClientesActivos;
        public int TotalClientesActivos { get => _totalClientesActivos; set { _totalClientesActivos = value; OnPropertyChanged(); } }

        private int _totalClientesInactivos;
        public int TotalClientesInactivos { get => _totalClientesInactivos; set { _totalClientesInactivos = value; OnPropertyChanged(); } }

        // --- VISIBILIDAD DE COLUMNAS ---
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
        // CONSTRUCTOR
        // =============================================================================
        public ClientesViewModel(ReportesService reportesService, FilterService filterService, IDialogService dialogService, INotificationService notificationService, BusinessLogicService businessLogic, SucursalesService sucursalesService)
        {
            _reportesService = reportesService;
            Filters = filterService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _logicService = new ClientesLogicService();
            _catalogoService = new CatalogoService(businessLogic);
            _sucursalesService = sucursalesService; // Asignado correctamente

            // Inicializar años
            int year = DateTime.Now.Year;
            AñosDisponibles = new ObservableCollection<int> { year, year - 1, year - 2, year - 3 };
            _anioSeleccionado = year;

            // Comandos
            ActualizarCommand = new RelayCommand(o => CargarDatosIniciales());
            ImprimirReporteCommand = new RelayCommand(o => GenerarPdfCliente());

            VerDetalleCommand = new RelayCommand(param => {
                if ( param is ClienteResumenModel cliente )
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
        // MÉTODOS DE CARGA DE DATOS
        // =============================================================================
        public async void CargarDatosIniciales()
        {
            // 1. GARANTÍA DE FILTROS: Si no hay sucursales, las cargamos de inmediato
            if ( Sucursales.Count == 0 )
            {
                Sucursales.Clear();
                // Opción segura para que el ComboBox no nazca vacío
                Sucursales.Add(new SucursalModel { Id = 0, Nombre = "0 - TODAS" });

                var dic = _sucursalesService.CargarSucursales();
                if ( dic != null )
                {
                    foreach ( var kvp in dic )
                        Sucursales.Add(new SucursalModel { Id = kvp.Key, Nombre = $"{kvp.Key} - {kvp.Value}" });
                }

                // Selección del default guardado
                int idGuardado = Properties.Settings.Default.SucursalDefaultId;
                var encontrada = Sucursales.FirstOrDefault(s => s.Id == idGuardado);

                // El setter de SucursalSeleccionada actualizará Filters.SucursalId
                SucursalSeleccionada = encontrada ?? Sucursales.First();
            }

            // 2. CARGA DE DATOS (SQL): Ahora que los filtros son seguros, traemos los datos
            IsLoading = true;
            try
            {
                string anioActualStr = AnioSeleccionado.ToString();
                string anioAnteriorStr = ( AnioSeleccionado - 1 ).ToString();
                string sucursalId = Filters.SucursalId.ToString();

                var taskActual = _reportesService.ObtenerHistoricoAnualPorArticulo(anioActualStr, sucursalId);
                var taskAnterior = _reportesService.ObtenerHistoricoAnualPorArticulo(anioAnteriorStr, sucursalId);

                await Task.WhenAll(taskActual, taskAnterior);

                _todosLosClientes = await Task.Run(() => _logicService.ProcesarClientes(taskActual.Result, taskAnterior.Result));

                TotalClientesActivos = _todosLosClientes.Count(x => x.VentaAnualActual > 0);
                TotalClientesInactivos = _todosLosClientes.Count(x => x.VentaAnualActual == 0 && x.VentaAnualAnterior > 0);

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

            // No ponemos IsLoading global aquí para no bloquear toda la UI al cambiar pestañitas, 
            // pero podrías poner una banderita local si quieres.

            try
            {
                var (inicio, fin) = ObtenerRangoFechas();

                var todosProductos = await _reportesService.ObtenerVariacionProductosCliente(
                    ClienteSeleccionado.Nombre,
                    inicio,
                    fin,
                    Filters.SucursalId);

                // Procesar Tops
                var declive = todosProductos.Where(x => x.Diferencia < 0).OrderBy(x => x.Diferencia).Take(10).ToList();
                var aumento = todosProductos.Where(x => x.Diferencia > 0).OrderByDescending(x => x.Diferencia).Take(10).ToList();

                ProductosEnDeclive = new ObservableCollection<ProductoAnalisisModel>(declive);
                ProductosEnAumento = new ObservableCollection<ProductoAnalisisModel>(aumento);

                OnPropertyChanged(nameof(ProductosEnDeclive));
                OnPropertyChanged(nameof(ProductosEnAumento));
            }
            catch ( Exception ex )
            {
                System.Diagnostics.Debug.WriteLine("Error cargando productos: " + ex.Message);
            }
        }

        private async void GenerarPdfCliente()
        {
            if ( ClienteSeleccionado == null ) return;

            string path = _dialogService.ShowSaveFileDialog("PDF Document|*.pdf", $"Reporte_{ClienteSeleccionado.Nombre}.pdf");

            if ( !string.IsNullOrEmpty(path) )
            {
                IsLoading = true;

                // Preparamos las listas auxiliares para evitar errores si son null
                var listAumento = ProductosEnAumento?.ToList() ?? new List<ProductoAnalisisModel>();
                var listDeclive = ProductosEnDeclive?.ToList() ?? new List<ProductoAnalisisModel>();

                await Task.Run(async () =>
                {
                    // 1. TRAER DATOS CRUDOS DE SQL (Solo traen Cantidad, no Litros)
                    var fin = DateTime.Now;
                    var inicio = fin.AddYears(-1);
                    var ventasRaw = await _reportesService.ObtenerVentasBrutasRango(Filters.SucursalId, inicio, fin);

                    // 2. FILTRAR POR CLIENTE
                    var movimientos = ventasRaw
                        .Where(x => x.Cliente == ClienteSeleccionado.Nombre)
                        .OrderByDescending(x => x.FechaEmision)
                        .Take(100)
                        .ToList();

                    // 3. ENRIQUECER DATOS (EL PASO MÁGICO ✨)
                    foreach ( var venta in movimientos )
                    {
                        // Preguntamos al catálogo: "¿Qué es este artículo?"
                        var info = _catalogoService.ObtenerInfo(venta.Articulo);

                        // A) Corregimos la Descripción (para que no salga el código feo)
                        venta.Descripcion = info.Descripcion;

                        venta.LitrosUnitarios = ( double ) info.Litros;

                        // C) Opcional: Si tu reporte usa la propiedad explícita 'LitrosTotal', llénala manual:
                        venta.LitrosTotal = venta.Cantidad * ( double ) info.Litros;
                    }

                    // 4. GENERAR PDF (Ahora sí lleva datos)
                    var exporter = new ExportService();
                    exporter.ExportarPdfCliente(
                        ClienteSeleccionado,
                        KpisDetalle,
                        movimientos,
                        listAumento,
                        listDeclive,
                        path
                    );
                });

                IsLoading = false;

                // Abrir archivo
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
            }
        }
        private (DateTime Inicio, DateTime Fin) ObtenerRangoFechas()
        {
            int anio = AnioSeleccionado;
            int mes = DateTime.Now.Month;

            if ( ModoSeleccionado == "Anual" )
            {
                return (new DateTime(anio, 1, 1), new DateTime(anio, 12, 31));
            }

            if ( ModoSeleccionado == "Semestral" )
            {

                bool esS2 = mes > 6;
                return esS2
                    ? (new DateTime(anio, 7, 1), new DateTime(anio, 12, 31))
                    : (new DateTime(anio, 1, 1), new DateTime(anio, 6, 30));
            }

            if ( ModoSeleccionado == "Trimestral" )
            {
                // Determinamos Q actual
                if ( mes <= 3 ) return (new DateTime(anio, 1, 1), new DateTime(anio, 3, 31));       // Q1
                if ( mes <= 6 ) return (new DateTime(anio, 4, 1), new DateTime(anio, 6, 30));       // Q2
                if ( mes <= 9 ) return (new DateTime(anio, 7, 1), new DateTime(anio, 9, 30));       // Q3
                return (new DateTime(anio, 10, 1), new DateTime(anio, 12, 31));                   // Q4
            }

            return (new DateTime(anio, 1, 1), new DateTime(anio, 12, 31));
        }
        private async void CargarDetalleAdicional(ClienteResumenModel cliente)
        {
            if ( cliente == null ) return;
            IsLoading = true;

            try
            {
                // KPIs (Se mantienen fijos anuales, o podrías hacerlos dinámicos igual)
                KpisDetalle = await _reportesService.ObtenerKpisCliente(cliente.Nombre, AnioSeleccionado, Filters.SucursalId);

                // Productos (Ahora son dinámicos)
                await CargarProductosDinamicos();
            }
            catch ( Exception ex )
            {
                _dialogService.ShowMessage("Error", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // =============================================================================
        // LÓGICA VISUAL Y GRÁFICAS
        // =============================================================================
        private void CalcularVisibilidadPeriodos()
        {
            // Lógica Exclusiva por Modo
            bool esTri = ModoSeleccionado == "Trimestral";
            bool esSem = ModoSeleccionado == "Semestral";
            // Si es "Anual", ambos serán false y se ocultará todo el detalle (dejando solo el total anual)

            // TRIMESTRES: Visibles solo si el modo es Trimestral
            VisibilityQ1 = esTri ? Visibility.Visible : Visibility.Collapsed;
            VisibilityQ2 = esTri ? Visibility.Visible : Visibility.Collapsed;
            VisibilityQ3 = esTri ? Visibility.Visible : Visibility.Collapsed;
            VisibilityQ4 = esTri ? Visibility.Visible : Visibility.Collapsed;

            // SEMESTRES: Visibles solo si el modo es Semestral
            VisibilityS1 = esSem ? Visibility.Visible : Visibility.Collapsed;
            VisibilityS2 = esSem ? Visibility.Visible : Visibility.Collapsed;

            // Notificar a la vista
            OnPropertyChanged(nameof(VisibilityQ1)); OnPropertyChanged(nameof(VisibilityQ2));
            OnPropertyChanged(nameof(VisibilityQ3)); OnPropertyChanged(nameof(VisibilityQ4));
            OnPropertyChanged(nameof(VisibilityS1)); OnPropertyChanged(nameof(VisibilityS2));
        }

        private void ActualizarGrafica()
        {
            // Solo dibujamos si hay cliente seleccionado y tiene historia
            if ( ClienteSeleccionado == null || ClienteSeleccionado.HistoriaMensualActual == null )
            {
                SeriesGrafica = null;
                return;
            }

            var historia = ClienteSeleccionado.HistoriaMensualActual;
            var valores = new List<decimal>();
            string[] etiquetas = null;

            // Agrupar datos según el modo de vista
            switch ( ModoSeleccionado )
            {
                case "Anual":
                    valores = historia; // 12 meses
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

            // Configurar LiveCharts
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

        // Método vital para evitar crashes al salir de la pantalla
        public void DetenerRenderizado()
        {
            SeriesGrafica = null; // Detiene el ticker de LiveCharts
        }

        private void FiltrarTabla()
        {
            if ( _todosLosClientes == null ) return;

            if ( string.IsNullOrWhiteSpace(TextoBusqueda) )
            {
                ClientesResumen = new ObservableCollection<ClienteResumenModel>(_todosLosClientes);
            }
            else
            {
                var filtrados = _todosLosClientes
                    .Where(x => x.Nombre.IndexOf(TextoBusqueda, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                ClientesResumen = new ObservableCollection<ClienteResumenModel>(filtrados);
            }
        }
    }
}