using System.Windows;
using System.Windows.Threading;

namespace MergeHelper
{
    public partial class App : Application
    {
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "MergeHelper – unerwarteter Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
