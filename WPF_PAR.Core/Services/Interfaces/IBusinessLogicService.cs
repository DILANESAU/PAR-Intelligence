using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.Core.Services.Interfaces
{
    public interface IBusinessLogicService
    {
        string NormalizarFamilia(string textoRaw);
        string ObtenerColorFamilia(string nombreFamilia);
    }
}
