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

using WPF_PAR.Core;
using WPF_PAR.MVVM.Models;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class DashboardViewModel : ObservableObject
    {
        private readonly ReportesService _reporteServices;
        private readonly SucursalesService _sucursalesService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService; // Inyectamos el servicio de notificaciones
        public FilterService Filters { get; }
        public ISnackbarMessageQueue ErrorMessageQueue { get; set; }

        // 1. PROPIEDADES Y COMANDOS
        public ObservableCollection<string> Periodos { get; set; } = new ObservableCollection<string>
        {
            "Esta Semana", "Este Mes", "Este Año"
        };

        private string _textoComparativo;
        public string TextoComparativo { get => _textoComparativo; set { _textoComparativo = value; OnPropertyChanged(); } }

        private bool _esCrecimientoPositivo;
        public bool EsCrecimientoPositivo { get => _esCrecimientoPositivo; set { _esCrecimientoPositivo = value; OnPropertyChanged(); } }

        private decimal _kpiVentasAnterior; // Para guardar el dato del mes pasado

        private string _periodoSeleccionado = "Este Mes";
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

                    // AGREGAR ESTO: Feedback inmediato de cambio de contexto
                    if ( value != null )
                    {
                        // Ejemplo: "Cargando datos de Matriz..."
                        // Usamos un mensaje corto que el ShowSuccess o ShowInfo muestre
                        Filters.SucursalId = value.Id;
                        _notificationService.ShowInfo($"Cambiando a: {value.Nombre}..."); 
                    }

                    if ( !_isLoading ) CargarDatos();
                }
            }
        }

        public RelayCommand ActualizarCommand { get; set; }

        // 2. KPIs
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

        // 3. GRÁFICOS
        private ISeries[] _seriesVentas;
        public ISeries[] SeriesVentas { get => _seriesVentas; set { _seriesVentas = value; OnPropertyChanged(); } }

        private Axis[] _ejeX;
        public Axis[] EjeX { get => _ejeX; set { _ejeX = value; OnPropertyChanged(); } }

        private Axis[] _ejeY;
        public Axis[] EjeY { get => _ejeY; set { _ejeY = value; OnPropertyChanged(); } }

        // 4. LISTAS
        public ObservableCollection<TopProductoItem> TopProductosList { get; set; }
        public ObservableCollection<ClienteRecienteItem> UltimosClientesList { get; set; }
        public ObservableCollection<VentaReporteModel> ListaVentas { get; set; }

        // 5. ESTADO
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        // CONSTRUCTOR
        public DashboardViewModel(ReportesService reportesServices, IDialogService dialogService, FilterService filterService, SucursalesService sucursalesService, INotificationService notificationService)
        {
            _dialogService = dialogService;
            Filters = filterService;
            _reporteServices = reportesServices;
            _sucursalesService = sucursalesService;
            _notificationService = notificationService;

            TopProductosList = new ObservableCollection<TopProductoItem>();
            UltimosClientesList = new ObservableCollection<ClienteRecienteItem>();
            ListaVentas = new ObservableCollection<VentaReporteModel>();
            Sucursales = new ObservableCollection<SucursalModel>();
            //ErrorMessageQueue = _notificationService.GetMessageQueue();
            CargarDatosIniciales();

            ActualizarCommand = new RelayCommand(o => CargarDatos());
            ConfigurarEjesIniciales();

            //CargarDatos();
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
            // 1. Cargamos sucursales solo si la lista está vacía
            if ( Sucursales.Count == 0 )
            {
                Sucursales.Clear();
                // Siempre incluir la opción global
                Sucursales.Add(new SucursalModel { Id = 0, Nombre = "0 - TODAS - Resumen Global" });

                // Usamos el servicio (que ya no lee el CSV, sino el código)
                var diccionario = _sucursalesService.CargarSucursales();
                if ( diccionario != null )
                {
                    foreach ( var item in diccionario )
                    {
                        Sucursales.Add(new SucursalModel { Id = item.Key, Nombre = $"{item.Key} - {item.Value}" });
                    }
                }

                // 2. Selección de la sucursal por defecto
                int guardada = Properties.Settings.Default.SucursalDefaultId;
                var encontrada = Sucursales.FirstOrDefault(s => s.Id == guardada);

                // El setter de SucursalSeleccionada disparará CargarDatos()
                SucursalSeleccionada = encontrada ?? Sucursales.First();
            }
        }
        private void CargarListaSucursales()
        {
            Sucursales.Clear();
            Sucursales.Add(new SucursalModel { Id = 0, Nombre = "TODAS - Resumen Global" });
            var diccionario = _sucursalesService.CargarSucursales();
            if ( diccionario == null || diccionario.Count == 0 )
            {

                _notificationService.ShowError("No se pudo cargar el catálogo de sucursales (archivo no encontrado).");
            }
            foreach ( var item in diccionario )
            {
                Sucursales.Add(new SucursalModel { Id = item.Key, Nombre = $"{item.Key} - {item.Value}" });
            }
        }

        public async void CargarDatos()
        {
            if ( IsLoading ) return;
            IsLoading = true;
            _notificationService.ShowInfo("Cargando datos del dashboard...");

            try
            {
                // ---------------------------------------------------------
                // A. CALCULAR FECHAS
                // ---------------------------------------------------------
                DateTime fechaInicio = DateTime.Now.Date;
                DateTime fechaFin = DateTime.Now.Date;
                DateTime fechaInicioAnt = DateTime.Now.Date;
                DateTime fechaFinAnt = DateTime.Now.Date;

                switch ( PeriodoSeleccionado )
                {
                    case "Esta Semana":
                        int diff = ( 7 + ( DateTime.Now.DayOfWeek - DayOfWeek.Monday ) ) % 7;
                        fechaInicio = DateTime.Now.AddDays(-1 * diff).Date;
                        // Ajuste: Fin de semana es hoy (o fin de semana real si prefieres)
                        fechaFin = DateTime.Now.Date;

                        fechaInicioAnt = fechaInicio.AddDays(-7);
                        fechaFinAnt = fechaFin.AddDays(-7);
                        break;

                    case "Este Mes":
                        fechaInicio = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                        fechaInicioAnt = fechaInicio.AddMonths(-1);
                        // El fin del periodo anterior debe ser el último día del mes pasado
                        // O el equivalente al día de hoy del mes pasado para ser justa la comparación
                        fechaFinAnt = fechaInicio.AddDays(-1);
                        break;

                    case "Este Año":
                        fechaInicio = new DateTime(DateTime.Now.Year, 1, 1);
                        fechaInicioAnt = fechaInicio.AddYears(-1);
                        fechaFinAnt = new DateTime(fechaInicio.Year - 1, 12, 31);
                        break;

                    default:
                        fechaInicio = DateTime.Today;
                        break;
                }

                int sucursalId = SucursalSeleccionada?.Id ?? 0;
                bool agruparPorMes = PeriodoSeleccionado == "Este Año";

                // ---------------------------------------------------------
                // B. EJECUCIÓN EN PARALELO (OPTIMIZACIÓN)
                // Lanzamos las 3 tareas al mismo tiempo a SQL
                // ---------------------------------------------------------

                // 1. Datos Actuales
                var taskActual = _reporteServices.ObtenerVentasRangoAsync(sucursalId, fechaInicio, fechaFin);

                // 2. Datos Anteriores (Comparativa)
                var taskAnterior = _reporteServices.ObtenerVentasRangoAsync(sucursalId, fechaInicioAnt, fechaFinAnt);

                // 3. Datos Gráfica
                var taskGrafico = _reporteServices.ObtenerTendenciaGrafica(sucursalId, fechaInicio, fechaFin, agruparPorMes);

                // Esperamos a que TODAS terminen (Más rápido que una por una)
                await Task.WhenAll(taskActual, taskAnterior, taskGrafico);

                // ---------------------------------------------------------
                // C. PROCESAR RESULTADOS
                // ---------------------------------------------------------
                var datosActuales = taskActual.Result;   // Usamos este resultado para TODO
                var datosAnteriores = taskAnterior.Result;
                var datosGrafico = taskGrafico.Result;

                // 1. Calcular Comparativa (KPIs)
                decimal ventaActual = datosActuales.Sum(x => x.TotalVenta);
                decimal ventaAnterior = datosAnteriores.Sum(x => x.TotalVenta);

                KpiVentas = ventaActual;
                KpiLitros = ( decimal )  datosActuales.Sum(x => x.LitrosTotal) ;

                if ( ventaAnterior == 0 )
                {
                    TextoComparativo = "Sin datos anteriores";
                    EsCrecimientoPositivo = true;
                }
                else
                {
                    decimal diferencia = ventaActual - ventaAnterior;
                    decimal porcentaje = ( diferencia / ventaAnterior ) * 100;

                    EsCrecimientoPositivo = porcentaje >= 0;
                    string signo = EsCrecimientoPositivo ? "+" : "";
                    TextoComparativo = $"{signo}{porcentaje:N1}% vs periodo anterior";
                }

                // 2. Actualizar Listas (Usando datosActuales, NO datosRango)
                ListaVentas.Clear();
                foreach ( var item in datosActuales ) ListaVentas.Add(item);

                ProcesarDatosResumen(datosActuales);

                // 3. Configurar Gráfico
                ConfigurarGraficoDinamico(datosGrafico, fechaInicio, fechaFin, PeriodoSeleccionado);

                // ---------------------------------------------------------
                // D. FEEDBACK AL USUARIO
                // ---------------------------------------------------------
                if ( datosActuales.Count == 0 )
                {
                    string nombreSucursal = SucursalSeleccionada?.Nombre.Split('-')[1].Trim() ?? "la sucursal";
                    _notificationService.ShowInfo($"No hay movimientos en {nombreSucursal} para {PeriodoSeleccionado.ToLower()}.");
                }
            }
            catch ( Exception ex )
            {
                _notificationService.ShowError($"Error al cargar dashboard: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _notificationService.ShowSuccess("Datos del dashboard actualizados.");
            }
        }

        private void ConfigurarGraficoDinamico(List<GraficoPuntoModel> datos, DateTime inicio, DateTime fin, string periodoTipo)
        {
            var valores = new List<decimal?>(); // Usamos decimal? (nullable) por si queremos romper la línea a futuro
            var etiquetas = new List<string>();

            bool isDark = false;
            try { isDark = Properties.Settings.Default.IsDarkMode; } catch { }
            var colorTexto = isDark ? SKColors.White : SKColors.DarkKhaki;

            // Líneas divisorias: Blanco muy transparente en Dark, Gris claro en Light
            var colorSeparador = isDark ? SKColors.White.WithAlpha(30) : SKColors.Gray.WithAlpha(30);

            if ( periodoTipo == "Este Año" )
            {
                // --- AÑO: 12 Meses fijos ---
                var nombresMeses = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
                for ( int i = 1; i <= 12; i++ )
                {
                    var dato = datos.FirstOrDefault(x => x.Indice == i);
                    valores.Add(dato?.Total ?? 0);
                    etiquetas.Add(nombresMeses[i - 1]);
                }
            }
            else if ( periodoTipo == "Esta Semana" )
            {
                // --- SEMANA: Siempre 7 días (Lunes a Domingo) ---
                // "inicio" ya viene calculado como el Lunes de esta semana desde CargarDatos()

                for ( int i = 0; i < 7; i++ )
                {
                    DateTime diaActualLoop = inicio.AddDays(i);

                    // Buscamos si hay ventas ese día del mes
                    var dato = datos.FirstOrDefault(x => x.Indice == diaActualLoop.Day);

                    // Agregamos el valor (0 si no hubo venta o si es futuro)
                    valores.Add(dato?.Total ?? 0);

                    // Etiqueta siempre visible: "Lun", "Mar", etc.
                    // Usamos formato "ddd" (Lun) para que sea corto y quepan todos
                    etiquetas.Add(diaActualLoop.ToString("ddd"));
                }
            }
            else
            {
                // --- MES: Todos los días del mes (1 al 30/31) ---
                int diasEnMes = DateTime.DaysInMonth(inicio.Year, inicio.Month);

                for ( int i = 1; i <= diasEnMes; i++ )
                {
                    var dato = datos.FirstOrDefault(x => x.Indice == i);
                    valores.Add(dato?.Total ?? 0);

                    // CAMBIO: Quitamos el "if (i % 2 == 0)"
                    // Ahora agregamos la etiqueta SIEMPRE.
                    // LiveCharts se encargará de ocultarlas si no caben visualmente, 
                    // pero el punto existirá y el Tooltip mostrará "13", "15", etc.
                    etiquetas.Add(i.ToString());
                }
            }

            // CREAR LA SERIE
            SeriesVentas = new ISeries[]
            {
        new LineSeries<decimal?> // Notar el '?' para coincidir con la lista
        {
            Name = "Ventas",
            Values = valores.ToArray(),
            Fill = new SolidColorPaint(SKColors.CornflowerBlue.WithAlpha(30)),
            Stroke = new SolidColorPaint(SKColors.CornflowerBlue) { StrokeThickness = 3 },
            GeometrySize = 8, // Tamaño del punto (círculo)
            GeometryStroke = new SolidColorPaint(isDark ? SKColors.Black : SKColors.White) { StrokeThickness = 2 },
            
            // Esto ayuda a que el tooltip sea más ágil
            LineSmoothness = 0.5
        }
            };

            // ACTUALIZAR EJES
            EjeX = new Axis[]
            {
        new Axis
        {
            Labels = etiquetas,
            LabelsPaint = new SolidColorPaint(colorTexto),
            TextSize = 12,
            // Si sientes que los números del mes se amontonan, descomenta esto:
            LabelsRotation = 0, 
        }
            };

            EjeY = new Axis[]
            {
        new Axis
        {
            Labeler = v => $"{v:C0}",
            LabelsPaint = new SolidColorPaint(colorTexto),
            TextSize = 12,
            SeparatorsPaint = new SolidColorPaint(colorSeparador)
        }
            };
        }

        private void ProcesarDatosResumen(List<VentaReporteModel> datos)
        {
            if ( datos == null || !datos.Any() )
            {
                KpiVentas = 0; KpiTransacciones = 0; KpiClientes = 0;
                TopProductosList.Clear();
                UltimosClientesList.Clear();
                return;
            }

            // A. KPIs (TotalVenta ya viene con negativos desde SQL)
            KpiVentas = datos.Sum(x => x.TotalVenta);
            KpiTransacciones = datos.Count;
            KpiClientes = datos.Select(x => x.Cliente).Distinct().Count();

            // B. TOP CLIENTES
            var topClientes = datos
                .GroupBy(x => x.Cliente)
                .Select(g => new TopProductoItem
                {
                    Nombre = string.IsNullOrEmpty(g.Key) ? "Público General" : g.Key,
                    Monto = g.Sum(x => x.TotalVenta)
                })
                .OrderByDescending(x => x.Monto)
                .Take(5)
                .ToList();

            for ( int i = 0; i < topClientes.Count; i++ ) topClientes[i].Ranking = i + 1;

            TopProductosList.Clear();
            foreach ( var item in topClientes ) TopProductosList.Add(item);

            // C. ÚLTIMOS CLIENTES
            var ultimos = datos
                .OrderByDescending(x => x.FechaEmision) // 1. Ordenamos por fecha (lo más nuevo arriba)
                .GroupBy(x => x.Cliente)              // 2. Agrupamos por Cliente
                .Select(g => g.First())               // 3. Tomamos SOLO la venta más reciente de ese cliente
                .Take(5)                              // 4. Ahora sí, tomamos los 5 mejores
                .Select(x => new ClienteRecienteItem
                {
                    Nombre = string.IsNullOrEmpty(x.Cliente) ? "Público General" : x.Cliente,
                    Fecha = x.FechaEmision,
                    Iniciales = ObtenerIniciales(x.Cliente)
                })
                .ToList();

            UltimosClientesList.Clear();
            foreach ( var item in ultimos ) UltimosClientesList.Add(item);
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

    // Modelos Auxiliares
    public class TopProductoItem { public int Ranking { get; set; } public string Nombre { get; set; } public decimal Monto { get; set; } }
    public class ClienteRecienteItem { public string Iniciales { get; set; } public string Nombre { get; set; } public DateTime Fecha { get; set; } }
    public class SucursalModel { public int Id { get; set; } public string Nombre { get; set; } }
}