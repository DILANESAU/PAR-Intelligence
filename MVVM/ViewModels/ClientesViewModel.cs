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
using WPF_PAR.Core.Services.Interfaces;

// 2. Apuntamos a los servicios propios de la UI (Dialogos, Notificaciones)
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;
using WPF_PAR.Converters;
using WPF_PAR.Core.Services; // Si usas ObservableObject desde aquí

namespace WPF_PAR.MVVM.ViewModels
{
    public class ClientesViewModel : ObservableObject
    {
        // --- SERVICIOS ---
        private readonly ReportesService _reportesService;
        private readonly IClientesLogicService _logicService;
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

        // --- FILTROS PARA LA GRÁFICA COMPARATIVA ---
        public ObservableCollection<AnioCheckModel> AniosFiltro { get; set; } = new ObservableCollection<AnioCheckModel>();

        private bool _verPorLitros;
        public bool VerPorLitros
        {
            get => _verPorLitros;
            set { _verPorLitros = value; OnPropertyChanged(); ActualizarGraficaComparativa(); }
        }

        private string _modoSeleccionado = "Mensual";
        public ObservableCollection<string> ModosVista { get; } = new ObservableCollection<string> { "Mensual", "Trimestral", "Semestral" };

        public string ModoSeleccionado
        {
            get => _modoSeleccionado;
            set
            {
                _modoSeleccionado = value;
                OnPropertyChanged();
                ActualizarGraficaComparativa(); // Dispara el recalculo dinámico
            }
        }

        // Aquí guardaremos los 5 años en crudo cuando seleccionen un cliente
        private List<HistoricoClienteModel> _datosHistoricosCrudos;

        // --- DATOS PRINCIPALES ---
        private List<ClienteAnalisisModel> _todosLosClientes;
        private ObservableCollection<ClienteAnalisisModel> _clientesResumen;
        public ObservableCollection<ClienteAnalisisModel> ClientesResumen
        {
            get => _clientesResumen;
            set { _clientesResumen = value; OnPropertyChanged(); }
        }

        // --- CLIENTE SELECCIONADO ---
        // --- CLIENTE SELECCIONADO ---
        private ClienteAnalisisModel _clienteSeleccionado;
        public ClienteAnalisisModel ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                _clienteSeleccionado = value;
                OnPropertyChanged();

                if (value != null)
                {
                    // 1. Encendemos el Dashboard de la derecha
                    EnModoDetalle = true;

                    // 2. Cargamos el historial y las tablas
                    CargarDetalleAdicional(value);
                }
                else
                {
                    // Si se deselecciona, regresamos a la pantalla de espera
                    EnModoDetalle = false;
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

        // MODELO PARA LOS CHECKBOXES DE AÑOS
        public class AnioCheckModel : ObservableObject
        {
            public int Anio { get; set; }
            private bool _isSelected;
            private Action _onChange;

            public AnioCheckModel(int anio, bool isSelected, Action onChange)
            {
                Anio = anio;
                _isSelected = isSelected;
                _onChange = onChange;
            }

            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(); _onChange?.Invoke(); }
            }
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
        public ClientesViewModel(ReportesService reportesService, ClientesService clientesService, SucursalesService sucursalesService, IClientesLogicService logicService, CatalogoService catalogoService, IDialogService dialogService, INotificationService notificationService, FilterService filterService)
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

            AniosFiltro.Add(new AnioCheckModel(year, true, ActualizarGraficaComparativa));
            AniosFiltro.Add(new AnioCheckModel(year - 1, true, ActualizarGraficaComparativa));
            AniosFiltro.Add(new AnioCheckModel(year - 2, false, ActualizarGraficaComparativa));
            AniosFiltro.Add(new AnioCheckModel(year - 3, false, ActualizarGraficaComparativa));

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
            if (ClienteSeleccionado == null) return;
            try
            {
                var todosProductos = await _clientesService.ObtenerVariacionProductos(
                    ClienteSeleccionado.Cliente,
                    AnioSeleccionado,
                    Filters.SucursalId);

                // 🛑 1. PROTECCIÓN ANTI-NULL: Si SQL no devuelve nada, mandamos listas vacías
                if (todosProductos == null || !todosProductos.Any())
                {
                    ProductosEnDeclive = new ObservableCollection<ProductoAnalisisModel>();
                    ProductosEnAumento = new ObservableCollection<ProductoAnalisisModel>();
                    return;
                }

                // Si sí hay datos, procesamos el top 10
                var declive = todosProductos.Where(x => (x.VentaActual - x.VentaAnterior) < 0).OrderBy(x => (x.VentaActual - x.VentaAnterior)).Take(10).ToList();
                var aumento = todosProductos.Where(x => (x.VentaActual - x.VentaAnterior) > 0).OrderByDescending(x => (x.VentaActual - x.VentaAnterior)).Take(10).ToList();

                ProductosEnDeclive = new ObservableCollection<ProductoAnalisisModel>(declive);
                ProductosEnAumento = new ObservableCollection<ProductoAnalisisModel>(aumento);
            }
            catch (Exception ex)
            {
                // 🚨 2. AHORA SÍ LO VEREMOS EN PANTALLA
                _notificationService.ShowError("Error SQL en Productos: " + ex.Message);
            }
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
                        venta.LitrosUnitarios = info.Litros;
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


        private async void CargarDetalleAdicional(ClienteAnalisisModel cliente)
        {
            if (cliente == null) return;
            IsLoading = true;

            try
            {
                // 1. CARGAR KPIs
                var kpis = await _clientesService.ObtenerKpisCliente(cliente.Cliente, AnioSeleccionado, Filters.SucursalId);

                // Si la BD regresó nulo, creamos uno vacío para que la UI no truene
                KpisDetalle = kpis ?? new KpiClienteModel();

                // 2. CARGAR PRODUCTOS (Con su nueva protección)
                await CargarProductosDinamicos();

                // 3. CARGAR HISTÓRICO Y GRÁFICA
                _datosHistoricosCrudos = await _clientesService.ObtenerHistorialCliente(cliente.Cliente, Filters.SucursalId);
                ActualizarGraficaComparativa();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("Fallo al consultar cliente", ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
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

       
        private void ActualizarGraficaComparativa()
        {
            if (_datosHistoricosCrudos == null || !_datosHistoricosCrudos.Any())
            {
                SeriesGrafica = null;
                return;
            }

            // 1. Ver qué Checkboxes están marcados
            var aniosActivos = AniosFiltro.Where(a => a.IsSelected).Select(a => a.Anio).ToList();
            if (!aniosActivos.Any()) { SeriesGrafica = null; return; }

            // 2. Filtrar los datos crudos
            var datosFiltrados = _datosHistoricosCrudos.Where(x => aniosActivos.Contains(x.Anio)).ToList();

            // 3. LA MAGIA: Agrupar por Mes, Trimestre o Semestre usando matemáticas
            var datosAgrupados = datosFiltrados.Select(x => new
            {
                x.Anio,
                x.Venta,
                x.Litros,
                Periodo = ModoSeleccionado == "Mensual" ? x.Mes :
                          ModoSeleccionado == "Trimestral" ? ((x.Mes - 1) / 3) + 1 :
                          ((x.Mes - 1) / 6) + 1 // Semestral
            })
            .GroupBy(x => new { x.Anio, x.Periodo })
            .Select(g => new
            {
                g.Key.Anio,
                g.Key.Periodo,
                Venta = g.Sum(v => v.Venta),
                Litros = g.Sum(l => l.Litros)
            }).ToList();

            // 4. Crear las Líneas de LiveCharts
            var nuevasSeries = new List<ISeries>();
            int periodosMaximos = ModoSeleccionado == "Mensual" ? 12 : ModoSeleccionado == "Trimestral" ? 4 : 2;

            foreach (var anio in aniosActivos.OrderByDescending(a => a))
            {
                // Creamos un arreglo lleno de ceros para evitar que la gráfica se descuadre si un mes no hubo ventas
                var valores = new double[periodosMaximos];
                var datosDelAnio = datosAgrupados.Where(x => x.Anio == anio).ToList();

                foreach (var d in datosDelAnio)
                {
                    if (d.Periodo >= 1 && d.Periodo <= periodosMaximos)
                    {
                        valores[d.Periodo - 1] = VerPorLitros ? (double)d.Litros : (double)d.Venta;
                    }
                }

                nuevasSeries.Add(new LineSeries<double>
                {
                    Values = valores,
                    Name = $"Año {anio}",
                    LineSmoothness = 0.5,
                    GeometrySize = 10,
                    Stroke = new SolidColorPaint { StrokeThickness = 3 },
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsFormatter = p => p.Model >= 1000000 ? $"{p.Model / 1000000:N1}M" : (p.Model >= 1000 ? $"{p.Model / 1000:N0}K" : $"{p.Model:N0}")
                });
            }

            SeriesGrafica = nuevasSeries.ToArray();

            // 5. Ajustar el Eje X
            string[] etiquetas = null;
            if (ModoSeleccionado == "Mensual") etiquetas = new[] { "ENE", "FEB", "MAR", "ABR", "MAY", "JUN", "JUL", "AGO", "SEP", "OCT", "NOV", "DIC" };
            else if (ModoSeleccionado == "Trimestral") etiquetas = new[] { "T1", "T2", "T3", "T4" };
            else if (ModoSeleccionado == "Semestral") etiquetas = new[] { "S1", "S2" };

            EjeXGrafica = new Axis[] { new Axis { Labels = etiquetas, LabelsRotation = 0, TextSize = 12 } };
            EjeYGrafica = new Axis[] { new Axis { Labeler = v => v >= 1000 ? $"{v / 1000:N0}K" : $"{v:N0}" } };

            OnPropertyChanged(nameof(SeriesGrafica));
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