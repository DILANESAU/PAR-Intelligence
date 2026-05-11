using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Text;
using System.Windows;
using WPF_PAR.Converters;
using WPF_PAR.Core.Models;
using WPF_PAR.Services;

namespace WPF_PAR.MVVM.ViewModels
{
    public class GestionUsuariosViewModel : ObservableObject
    {
        private readonly UsuariosService _usuariosService;

        // La lista que se mostrará en el DataGrid
        private ObservableCollection<UsuarioModel> _listaUsuarios;
        public ObservableCollection<UsuarioModel> ListaUsuarios
        {
            get => _listaUsuarios;
            set { _listaUsuarios = value; OnPropertyChanged(); }
        }

        // Comandos para los botones de acción en la tabla
        public RelayCommand CargarUsuariosCommand { get; }
        public RelayCommand AlternarEstadoCommand { get; }
        public RelayCommand ResetPasswordCommand { get; }

        public GestionUsuariosViewModel()
        {
            string connString = System.Configuration.ConfigurationManager.ConnectionStrings["ParSystem"]?.ConnectionString;
            _usuariosService = new UsuariosService(connString);
            ListaUsuarios = new ObservableCollection<UsuarioModel>();

            CargarUsuariosCommand = new RelayCommand(async o => await CargarDatosAsync());
            AlternarEstadoCommand = new RelayCommand(async param => await AlternarEstado(param));
            ResetPasswordCommand = new RelayCommand(async param => await ResetearPassword(param));

            // Cargamos la lista al iniciar
            CargarUsuariosCommand.Execute(null);
        }

        private async Task CargarDatosAsync()
        {
            var usuarios = await _usuariosService.ObtenerUsuariosAsync();
            ListaUsuarios = new ObservableCollection<UsuarioModel>(usuarios);
        }

        private async Task AlternarEstado(object param)
        {
            if (param is UsuarioModel usuario)
            {
                // Preguntamos si está seguro
                string accion = usuario.Activo ? "DESACTIVAR" : "ACTIVAR";
                var result = MessageBox.Show($"¿Estás seguro de {accion} al usuario {usuario.Username}?", "Confirmación", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    bool nuevoEstado = !usuario.Activo;
                    bool exito = await _usuariosService.CambiarEstadoAsync(usuario.Username, nuevoEstado);

                    if (exito)
                    {
                        usuario.Activo = nuevoEstado;
                        // Notificamos a la UI que cambió el modelo (opcional si hereda ObservableObject)
                        await CargarDatosAsync(); // Recargamos para refrescar la tabla
                    }
                }
            }
        }

        private async Task ResetearPassword(object param)
        {
            if (param is UsuarioModel usuario)
            {
                var result = MessageBox.Show($"¿Deseas generar una contraseña temporal para {usuario.Username}?\n\nLa contraseña será: {usuario.Username}", "Resetear Contraseña", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    string claveTemporal = usuario.Username; // Puedes cambiar esto por lo que gustes
                    bool exito = await _usuariosService.ResetearPasswordAsync(usuario.Username, claveTemporal);

                    if (exito)
                        MessageBox.Show($"Se ha restablecido la contraseña de {usuario.Username}.\nAl ingresar se le pedirá cambiarla.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Hubo un error al restablecer la contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
