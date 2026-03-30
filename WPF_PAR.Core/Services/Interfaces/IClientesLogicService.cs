using System;
using System.Collections.Generic;
using System.Text;
using WPF_PAR.Core.Models;

namespace WPF_PAR.Core.Services.Interfaces
{
    public interface IClientesLogicService
    {
        List<ClienteResumenModel> ProcesarClientes(List<VentaReporteModel> ventasActuales, List<VentaReporteModel> ventasAnteriores);
    }
}
