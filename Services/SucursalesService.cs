using System;
using System.Collections.Generic;
using System.Linq;

namespace WPF_PAR.Services
{
    public class SucursalesService
    {
        public Dictionary<int, string> CargarSucursales()
        {
            // DATOS INCRUSTADOS: Ya no dependemos de archivos externos
            var datos = new Dictionary<int, string>
            {
                { 1210, "PAR Cuauhtemoc Veracruz" },
                { 1212, "PAR Altamira" },
                { 1306, "PAR Puebla Camino Real" },
                { 1309, "PAR Teran Tuxtla" },
                { 1312, "PAR Ciudad Judicial" },
                { 1313, "PAR Cuautla" },
                { 1323, "PAR Xola" },
                { 1510, "PAR Comitan" },
                { 1512, "PAR Huatulco" },
                { 2801, "PAR Pinturas" },
                { 3501, "PAR Acajete" },
                { 4601, "PAR Acapulco" },
                { 4701, "PAR Platino" },
                { 5101, "PMJ Pinturerias" },
                { 5301, "PAR Universidades" },
                { 5401, "PAR 31 Poniente" },
                { 5402, "PAR Tapachula" },
                { 5601, "PAR Bugambilias" },
                { 5801, "PAR 9na Sur" },
                { 5901, "PAR Ecatepec" },
                { 6000, "PAR Oaxaca" },
                { 7001, "PAR Merida" },
                { 8001, "PAR Misiones" }
            };

            // Lo devolvemos ordenado por ID (Key)
            return datos.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        }
    }
}