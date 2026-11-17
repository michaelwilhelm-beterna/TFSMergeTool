using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MergeHelper
{
    public partial class ConnectionDialog : Window
    {
        private readonly ConnectionConfig _original;
        public ConnectionConfig Result { get; private set; }

        public ConnectionDialog(ConnectionConfig existing)
        {
            InitializeComponent();
            _original = existing;
            if (existing != null)
            {
                Title = "Verbindung bearbeiten";
                NameBox.Text = existing.Name;
                UrlBox.Text = existing.Url;
                TenantBox.Text = existing.TenantId;
                PatRadio.IsChecked = existing.AuthMode == AuthMode.Pat;
                EntraRadio.IsChecked = existing.AuthMode == AuthMode.Entra;
                if (!string.IsNullOrEmpty(existing.PatEncrypted))
                    PatHint.Text = "PAT ist gespeichert. Leer lassen, um ihn beizubehalten. " + PatHint.Text;
            }
            else
            {
                Title = "Neue Verbindung";
                UrlBox.Text = "https://dev.azure.com/";
            }
            UpdateAuthFields();
            Loaded += (s, e) => NameBox.Focus();
        }

        private void Auth_Changed(object sender, RoutedEventArgs e) => UpdateAuthFields();

        private void UpdateAuthFields()
        {
            if (PatBox == null || TenantBox == null) return;
            var pat = PatRadio.IsChecked == true;
            PatBox.IsEnabled = pat;
            PatLabel.Opacity = pat ? 1 : 0.5;
            TenantBox.IsEnabled = !pat;
            TenantLabel.Opacity = pat ? 0.5 : 1;
        }

        private ConnectionConfig BuildConfig()
        {
            var name = NameBox.Text.Trim();
            var url = UrlBox.Text.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Bitte einen Namen angeben.");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new InvalidOperationException("Bitte eine gültige URL angeben, z.B. https://dev.azure.com/meineorganisation");

            var c = _original?.CloneSettings() ?? new ConnectionConfig();
            c.Name = name;
            c.Url = url;
            c.AuthMode = PatRadio.IsChecked == true ? AuthMode.Pat : AuthMode.Entra;
            c.TenantId = TenantBox.Text.Trim();
            if (!string.IsNullOrEmpty(PatBox.Password))
                c.PatEncrypted = ConfigStore.Protect(PatBox.Password.Trim());
            if (c.AuthMode == AuthMode.Pat && string.IsNullOrEmpty(c.PatEncrypted))
                throw new InvalidOperationException("Bitte einen PAT eingeben.");
            return c;
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            ConnectionConfig c;
            try { c = BuildConfig(); }
            catch (Exception ex) { ShowResult(ex.Message, false); return; }

            TestButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            ShowResult("Verbinde …", null);
            try
            {
                using (var session = await TfsSession.OpenAsync(c, new WindowInteropHelper(this).Handle))
                    ShowResult("✔ Verbindung erfolgreich. Angemeldet als: " + session.AuthorizedUser, true);
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += "\n" + ex.InnerException.Message;
                ShowResult("✖ " + msg, false);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                TestButton.IsEnabled = true;
            }
        }

        private void ShowResult(string text, bool? ok)
        {
            TestResult.Text = text;
            TestResult.Foreground = ok == true ? Brushes.DarkGreen : ok == false ? Brushes.DarkRed : Brushes.Gray;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Result = BuildConfig();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowResult(ex.Message, false);
            }
        }
    }
}
