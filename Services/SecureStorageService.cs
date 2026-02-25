using CredentialManagement;

using System;

namespace WPF_PAR.Services
{
    public class SecureStorageService
    {
        // Llaves para el Administrador de Credenciales de Windows
        public const string KeyAuth = "PAR_System_AuthPass";
        public const string KeyData = "PAR_System_DataPass";

        public void GuardarPassword(string password, string target)
        {
            if ( string.IsNullOrEmpty(password) ) return;

            using ( var cred = new Credential() )
            {
                cred.Password = password;
                cred.Target = target;
                cred.Type = CredentialType.Generic;
                cred.PersistanceType = PersistanceType.LocalComputer;
                cred.Save();
            }
        }

        // Renombrado a ObtenerPassword para coincidir con la llamada del ViewModel
        public string RecuperarPassword(string target)
        {
            using ( var cred = new Credential() )
            {
                cred.Target = target;
                if ( cred.Load() )
                    return cred.Password;

                return string.Empty;
            }
        }
    }
}