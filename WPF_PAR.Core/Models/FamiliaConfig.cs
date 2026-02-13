using System;
using System.Collections.Generic;
using System.Text;

namespace WPF_PAR.MVVM.Models
{
    public class FamiliaConfig
    {
        public string NombreNormalizado { get; set; }
        public string ColorHex { get; set; }
        public List<string> PalabrasClave {  get; set; }

        public FamiliaConfig() { PalabrasClave = new List<string>(); }
    }
}
