using CredentialManagement;

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace WPF_PAR.Services
{
    public class SecureStorageService
    {
        // Definimos las dos llaves constantes
        public const string KeyAuth = "PAR_System_AuthPass";
        public const string KeyData = "PAR_System_DataPass";

        // Ahora pedimos el 'target' (KeyAuth o KeyData)
        public void GuardarPassword(string password, string target)
        {
            using ( var cred = new Credential() )
            {
                cred.Password = password;
                cred.Target = target; // <--- Dinámico
                cred.Type = CredentialType.Generic;
                cred.PersistanceType = PersistanceType.LocalComputer;
                cred.Save();
            }
        }

        public string RecuperarPassword(string target)
        {
            using ( var cred = new Credential() )
            {
                cred.Target = target; // <--- Dinámico
                if ( cred.Load() ) return cred.Password;
                return string.Empty;
            }
        }
    }
}
