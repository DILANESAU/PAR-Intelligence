        using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace WPF_PAR.Core
{
    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        // La propiedad "Data" guardará nuestro ViewModel
        public object Data
        {
            get { return ( object ) GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new PropertyMetadata(null));
    }
}
