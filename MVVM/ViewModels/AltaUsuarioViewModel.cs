using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Text;
using WPF_PAR.Converters;
using WPF_PAR.Core.Models;
using WPF_PAR.Core.Services;
using WPF_PAR.Services;
using WPF_PAR.Services.Interfaces;

namespace WPF_PAR.MVVM.ViewModels
{
    public class AltaUsuarioViewModel : ObservableObject
    {
        private string _nombre;
        public string Nombre { get => _nombre; set { _nombre = value; OnPropertyChanged(); } }

        private string _usuarioLogin;
        public string UsuarioLogin { get => _usuarioLogin; set { _usuarioLogin = value; OnPropertyChanged(); } }

        private string _password;
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }


        public ObservableCollection<SucursalSeleccionable> SucursalesDisponibles { get; set; }

        private string _rolSeleccionado;
        public string RolSeleccionado { get => _rolSeleccionado; set { _rolSeleccionado = value; OnPropertyChanged(); } }
        private readonly INotificationService _notificationService;

        public ObservableCollection<string> RolesDisponibles { get; set; } = new ObservableCollection<string>
        {
            "Distribuidor", "Administrador", "Sistemas", "Director"
        };

        // Comandos de la ventanita
        public RelayCommand GuardarCommand { get; set; }
        public RelayCommand CancelarCommand { get; set; }

        public AltaUsuarioViewModel()
        {
            RolSeleccionado = "Distribuidor"; 
            _notificationService = new NotificationService();

            CargarSucursales();

            GuardarCommand = new RelayCommand(o => GuardarUsuario());
        }
        private void CargarSucursales()
        {
            SucursalesDisponibles = new ObservableCollection<SucursalSeleccionable>();
            // Usamos el servicio que ya tienes para traer las sucursales reales de la Mini PC
            var service = new SucursalesService(System.Configuration.ConfigurationManager.ConnectionStrings["ParSystem"].ConnectionString);
            var lista = service.CargarSucursales();

            foreach (var item in lista)
            {
                if (item.Key != 0) // Ignoramos el "0 - TODAS" para el alta individual
                    SucursalesDisponibles.Add(new SucursalSeleccionable { Id = item.Key, Nombre = item.Value });
            }
        }
        private async void GuardarUsuario()
        {
            // 1. Validaciones básicas
            if (string.IsNullOrWhiteSpace(Nombre) || string.IsNullOrWhiteSpace(UsuarioLogin) || string.IsNullOrWhiteSpace(Password))
            {
                System.Windows.MessageBox.Show("Por favor, llena todos los campos.", "Aviso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 2. Validar que elijan sucursales si es Distribuidor
            var seleccionadas = SucursalesDisponibles.Where(s => s.IsSelected).ToList();
            if (seleccionadas.Count == 0 && RolSeleccionado == "Distribuidor")
            {
                System.Windows.MessageBox.Show("Por favor selecciona al menos una sucursal para este Distribuidor.", "Aviso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            int idDelRol = 1;
            switch (RolSeleccionado)
            {
                case "Administrador": idDelRol = 1; break;
                case "Sistemas": idDelRol = 2; break;
                case "Director": idDelRol = 3; break;
                case "Distribuidor": idDelRol = 4; break;
            }

            try
            {
                string connString = System.Configuration.ConfigurationManager.ConnectionStrings["ParSystem"]?.ConnectionString;
                var sqlHelper = new WPF_PAR.Core.Services.SqlHelper(connString);

                // 🔒 Encriptamos la contraseña
                string passwordEncriptado = WPF_PAR.Services.PasswordHasher.HashPassword(this.Password);

                // 👇 EL TRUCO: Agregamos "OUTPUT INSERTED.Id" para recuperar la llave primaria
                string query = @"
                    INSERT INTO Usuarios (NombreCompleto, Username, PasswordHash, RolId, Activo, FechaCreacion, RequiereCambioPwd) 
                    OUTPUT INSERTED.Id
                    VALUES (@Nombre, @Username, @Password, @RolId, 1, GETDATE(), 1)";

                var resultadoQuery = await sqlHelper.QueryAsync<int>(query, new
                {
                    Nombre = this.Nombre,
                    Username = this.UsuarioLogin,
                    Password = passwordEncriptado,
                    RolId = idDelRol
                });

                // Sacamos el número de esa lista (que solo traerá 1 resultado)
                int nuevoUsuarioId = resultadoQuery.FirstOrDefault();
                // 👇 ¡AHORA SÍ! LLAMAMOS AL MÉTODO PARA GUARDAR LAS SUCURSALES
                await GuardarRelacionSucursales(nuevoUsuarioId, sqlHelper);

                System.Windows.MessageBox.Show($"Usuario {UsuarioLogin} creado exitosamente.\nSe le solicitará cambio de clave al ingresar.", "Éxito", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(true, null);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show("Error crítico al guardar:\n\n" + ex.Message, "Error de Base de Datos", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        private async Task GuardarRelacionSucursales(int idUsuarioRecienCreado, SqlHelper helper)
        {
            var seleccionadas = SucursalesDisponibles.Where(s => s.IsSelected);
            foreach (var suc in seleccionadas)
            {
                string query = "INSERT INTO UsuarioSucursales (IdUsuario, SucursalId) VALUES (@User, @Suc)";
                await helper.ExecuteAsync(query, new { User = idUsuarioRecienCreado, Suc = suc.Id });
            }
        }

    }

}

public class SucursalSeleccionable : ObservableObject
{
    public int Id { get; set; }
    public string Nombre { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }
}
