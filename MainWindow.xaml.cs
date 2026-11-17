using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MergeHelper
{
    public partial class MainWindow : Window
    {
        private AppConfig _config;
        private readonly ObservableCollection<ChangesetRow> _rows = new ObservableCollection<ChangesetRow>();
        private readonly ICollectionView _view;
        private readonly Dictionary<string, string> _filters = new Dictionary<string, string>();
        private readonly Dictionary<string, TextBox> _filterBoxes = new Dictionary<string, TextBox>();
        private readonly DispatcherTimer _filterTimer;
        private readonly Dictionary<Guid, TfsSession> _sessions = new Dictionary<Guid, TfsSession>();
        private ConnectionConfig _loadedConnection;
        private BranchPair _loadedPair;
        private bool _busy;
        private bool _restoring;

        public MainWindow()
        {
            InitializeComponent();

            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = RowFilter;
            _view.SortDescriptions.Add(new SortDescription(nameof(ChangesetRow.Id), ListSortDirection.Ascending));
            ChangesetGrid.ItemsSource = _view;

            _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _filterTimer.Tick += (s, e) => { _filterTimer.Stop(); _view.Refresh(); UpdateCount(); };
        }

        private IntPtr Hwnd => new WindowInteropHelper(this).Handle;

        private ConnectionConfig SelectedConnection => ConnectionList.SelectedItem as ConnectionConfig;
        private BranchPair SelectedPair => PairList.SelectedItem as BranchPair;

        // ---------------------------------------------------------------- Start / Konfiguration

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _config = ConfigStore.Load();
            _restoring = true;
            RefreshConnectionList();
            var last = _config.Connections.FirstOrDefault(c => c.Id == _config.LastConnectionId) ?? _config.Connections.FirstOrDefault();
            ConnectionList.SelectedItem = last;
            if (last != null && _config.LastPairIndex >= 0 && _config.LastPairIndex < last.BranchPairs.Count)
                PairList.SelectedIndex = _config.LastPairIndex;
            _restoring = false;
            UpdateButtons();
            UpdateCount();
            SetStatus(_config.Connections.Count == 0
                ? "Lege oben links eine Verbindung an (z.B. https://dev.azure.com/<organisation>)."
                : "Bereit. Konfiguration: " + ConfigStore.FilePath);
        }

        private void SaveConfig()
        {
            _config.LastConnectionId = SelectedConnection?.Id;
            _config.LastPairIndex = PairList.SelectedIndex;
            try { ConfigStore.Save(_config); }
            catch (Exception ex) { ShowError("Konfiguration konnte nicht gespeichert werden", ex); }
        }

        private void RefreshConnectionList()
        {
            var sel = SelectedConnection;
            ConnectionList.ItemsSource = null;
            ConnectionList.ItemsSource = _config.Connections;
            if (sel != null) ConnectionList.SelectedItem = _config.Connections.FirstOrDefault(c => c.Id == sel.Id);
        }

        private void RefreshPairList(int selectIndex = -1)
        {
            PairList.ItemsSource = null;
            PairList.ItemsSource = SelectedConnection?.BranchPairs;
            if (selectIndex >= 0 && PairList.Items.Count > selectIndex) PairList.SelectedIndex = selectIndex;
            else if (PairList.Items.Count > 0) PairList.SelectedIndex = 0;
        }

        private void ConnectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshPairList();
            UpdateButtons();
            if (!_restoring) SaveConfig();
        }

        private void PairList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
            if (!_restoring) SaveConfig();
        }

        private void AddConnection_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ConnectionDialog(null) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            _config.Connections.Add(dlg.Result);
            RefreshConnectionList();
            ConnectionList.SelectedItem = dlg.Result;
            SaveConfig();
        }

        private void EditConnection_Click(object sender, RoutedEventArgs e)
        {
            var conn = SelectedConnection;
            if (conn == null || _busy) return;
            var dlg = new ConnectionDialog(conn) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            var idx = _config.Connections.IndexOf(conn);
            _config.Connections[idx] = dlg.Result;
            DropSession(conn.Id);
            var pairIdx = PairList.SelectedIndex;
            RefreshConnectionList();
            ConnectionList.SelectedItem = dlg.Result;
            RefreshPairList(pairIdx);
            SaveConfig();
        }

        private void DeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            var conn = SelectedConnection;
            if (conn == null) return;
            if (MessageBox.Show(this, $"Verbindung \"{conn.Name}\" inkl. {conn.BranchPairs.Count} Branch-Paar(en) löschen?",
                    "Löschen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _config.Connections.Remove(conn);
            DropSession(conn.Id);
            RefreshConnectionList();
            ConnectionList.SelectedItem = _config.Connections.FirstOrDefault();
            SaveConfig();
        }

        private async void AddPair_Click(object sender, RoutedEventArgs e)
        {
            var conn = SelectedConnection;
            if (conn == null) return;
            var pair = await EditPairAsync(conn, null);
            if (pair == null) return;
            conn.BranchPairs.Add(pair);
            RefreshPairList(conn.BranchPairs.Count - 1);
            SaveConfig();
        }

        private async void EditPair_Click(object sender, RoutedEventArgs e)
        {
            var conn = SelectedConnection;
            var old = SelectedPair;
            if (conn == null || old == null) return;
            var pair = await EditPairAsync(conn, old);
            if (pair == null) return;
            var idx = conn.BranchPairs.IndexOf(old);
            conn.BranchPairs[idx] = pair;
            RefreshPairList(idx);
            SaveConfig();
        }

        private async Task<BranchPair> EditPairAsync(ConnectionConfig conn, BranchPair existing)
        {
            List<BranchInfo> branches = null;
            await RunBusy("Lade Branches vom Server …", async () =>
            {
                var session = await GetSessionAsync(conn);
                branches = await Task.Run(() => session.GetBranches());
            });
            if (branches == null) return null;
            if (branches.Count == 0)
            {
                MessageBox.Show(this, "Auf dem Server wurden keine TFVC-Branches gefunden.", "Branches",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
            SetStatus("Bereit");
            var dlg = new BranchPairDialog(branches, existing) { Owner = this };
            return dlg.ShowDialog() == true ? dlg.Result : null;
        }

        private void DeletePair_Click(object sender, RoutedEventArgs e)
        {
            var conn = SelectedConnection;
            var pair = SelectedPair;
            if (conn == null || pair == null) return;
            if (MessageBox.Show(this, $"Branch-Paar löschen?\n\n{pair.Source}\n→ {pair.Target}", "Löschen",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var idx = PairList.SelectedIndex;
            conn.BranchPairs.Remove(pair);
            RefreshPairList(Math.Max(0, idx - 1));
            SaveConfig();
        }

        // ---------------------------------------------------------------- Laden

        private void PairList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedPair != null) Load_Click(sender, e);
        }

        private async void Load_Click(object sender, RoutedEventArgs e)
        {
            var conn = SelectedConnection;
            var pair = SelectedPair;
            if (conn == null || pair == null) return;
            await LoadCandidatesAsync(conn, pair);
        }

        private async void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (_loadedConnection != null && _loadedPair != null)
                await LoadCandidatesAsync(_loadedConnection, _loadedPair);
        }

        private async Task LoadCandidatesAsync(ConnectionConfig conn, BranchPair pair)
        {
            List<ChangesetRow> result = null;
            await RunBusy($"Lade Merge-Kandidaten {pair.Source} → {pair.Target} …", async () =>
            {
                var session = await GetSessionAsync(conn);
                result = await Task.Run(() => session.GetMergeCandidates(pair.Source, pair.Target));
            });
            if (result == null) return;

            _rows.Clear();
            foreach (var r in result) _rows.Add(r);
            _loadedConnection = conn;
            _loadedPair = pair;
            CurrentPairText.Text = $"{conn.Name}:  {pair.Source}  →  {pair.Target}";
            SetStatus($"{result.Count} Merge-Kandidaten geladen ({DateTime.Now:HH:mm:ss}).");
            UpdateCount();
            UpdateButtons();
        }

        // ---------------------------------------------------------------- Filter

        private void FilterBox_Loaded(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            var key = tb.Tag as string;
            if (key == null) return;
            _filterBoxes[key] = tb;
            if (_filters.TryGetValue(key, out var v) && tb.Text != v) tb.Text = v;
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = (TextBox)sender;
            if (!(tb.Tag is string key)) return;
            _filters[key] = tb.Text;
            _filterTimer.Stop();
            _filterTimer.Start();
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            _filters.Clear();
            foreach (var tb in _filterBoxes.Values) tb.Text = "";
            _view.Refresh();
            UpdateCount();
        }

        private string Filter(string key) => _filters.TryGetValue(key, out var v) ? v : null;

        private bool RowFilter(object o)
        {
            var r = (ChangesetRow)o;
            return FilterLogic.MatchNumber(r.Id, Filter("Changeset"))
                   && FilterLogic.MatchText(r.User, Filter("Benutzer"))
                   && FilterLogic.MatchDate(r.Date, r.DateText, Filter("Datum"))
                   && FilterLogic.MatchText(r.Comment, Filter("Kommentar"));
        }

        // ---------------------------------------------------------------- Aktionen im Grid

        private List<ChangesetRow> SelectedRows =>
            ChangesetGrid.SelectedItems.OfType<ChangesetRow>().OrderBy(r => r.Id).ToList();

        private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
            UpdateCount();
        }

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is DataGridRow)) dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            if (dep is DataGridRow) Details_Click(sender, e);
        }

        private void CopyIds_Click(object sender, RoutedEventArgs e)
        {
            var rows = SelectedRows;
            if (rows.Count == 0) return;
            var text = string.Join(", ", rows.Select(r => r.Id));
            Clipboard.SetText(text);
            SetStatus($"{rows.Count} Changeset-Nummer(n) kopiert: {Truncate(text, 120)}");
        }

        private void CopyRows_Click(object sender, RoutedEventArgs e)
        {
            var rows = SelectedRows;
            if (rows.Count == 0) return;
            var sb = new StringBuilder("Changeset\tBenutzer\tDatum\tKommentar\r\n");
            foreach (var r in rows)
                sb.Append(r.Id).Append('\t').Append(r.User).Append('\t').Append(r.DateText).Append('\t')
                  .Append(r.CommentSingleLine).Append("\r\n");
            Clipboard.SetText(sb.ToString());
            SetStatus($"{rows.Count} Zeile(n) kopiert.");
        }

        private async void Details_Click(object sender, RoutedEventArgs e)
        {
            var row = ChangesetGrid.SelectedItem as ChangesetRow ?? SelectedRows.FirstOrDefault();
            if (row == null || _loadedConnection == null) return;

            (ChangesetRow Info, List<ChangeRow> Changes) details = default;
            var ok = false;
            await RunBusy($"Lade Details zu C{row.Id} …", async () =>
            {
                var session = await GetSessionAsync(_loadedConnection);
                details = await Task.Run(() => session.GetChangesetDetails(row.Id));
                ok = true;
            });
            if (!ok) return;
            SetStatus("Bereit");
            new ChangesetDetailsWindow(details.Info, details.Changes) { Owner = this }.Show();
        }

        private async void Merge_Click(object sender, RoutedEventArgs e)
        {
            var rows = SelectedRows;
            if (rows.Count == 0 || _loadedConnection == null || _loadedPair == null) return;
            var conn = _loadedConnection;
            var pair = _loadedPair;

            Microsoft.TeamFoundation.VersionControl.Client.Workspace ws = null;
            await RunBusy("Suche lokalen Workspace …", async () =>
            {
                var session = await GetSessionAsync(conn);
                ws = await Task.Run(() => session.FindWorkspaceFor(pair.Target));
                if (ws == null)
                    throw new InvalidOperationException(
                        $"Auf diesem Rechner ({Environment.MachineName}) wurde kein Workspace gefunden, der den Ziel-Branch\n{pair.Target}\ngemappt hat.\n\nBitte zuerst in Visual Studio einen Workspace mit diesem Mapping anlegen.");
            });
            if (ws == null) return;
            SetStatus("Bereit");

            var idList = string.Join(", ", rows.Select(r => r.Id));
            var msg = $"{rows.Count} Changeset(s) einzeln (aufsteigend) mergen?\n\n" +
                      $"Quelle:     {pair.Source}\nZiel:          {pair.Target}\nWorkspace: {ws.Name}\n\n" +
                      $"Changesets: {Truncate(idList, 400)}\n\n" +
                      "Es werden nur ausstehende Änderungen erzeugt – Konflikte auflösen und Einchecken erfolgen in Visual Studio.";
            if (MessageBox.Show(this, msg, "Mergen", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            string log = null;
            await RunBusy($"Merge {rows.Count} Changeset(s) …", async () =>
            {
                var session = await GetSessionAsync(conn);
                var ids = rows.Select(r => r.Id).ToList();
                log = await Task.Run(() => session.Merge(ws, pair.Source, pair.Target, ids));
            });
            if (log == null) return;

            new TextWindow("Merge-Ergebnis", log) { Owner = this }.ShowDialog();
            SetStatus("Merge abgeschlossen. Hinweis: Merge-Kandidaten ändern sich erst nach dem Einchecken.");
        }

        // ---------------------------------------------------------------- Hilfsfunktionen

        private async Task<TfsSession> GetSessionAsync(ConnectionConfig conn)
        {
            if (_sessions.TryGetValue(conn.Id, out var s))
            {
                // Entra-Token läuft nach ca. 60 Min. ab -> rechtzeitig neu verbinden.
                if (conn.AuthMode == AuthMode.Pat || DateTime.UtcNow - s.CreatedUtc < TimeSpan.FromMinutes(40))
                    return s;
                DropSession(conn.Id);
            }
            s = await TfsSession.OpenAsync(conn, Hwnd);
            _sessions[conn.Id] = s;
            return s;
        }

        private void DropSession(Guid id)
        {
            if (_sessions.TryGetValue(id, out var s))
            {
                _sessions.Remove(id);
                try { s.Dispose(); } catch { /* egal */ }
            }
        }

        private async Task RunBusy(string status, Func<Task> action)
        {
            if (_busy) return;
            _busy = true;
            UpdateButtons();
            SetStatus(status);
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                if (SelectedConnection != null && !(ex is InvalidOperationException)) DropSession(SelectedConnection.Id);
                SetStatus("Fehler: " + ex.Message);
                ShowError(status.TrimEnd('…', ' ') + " fehlgeschlagen", ex);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                _busy = false;
                UpdateButtons();
            }
        }

        private void UpdateButtons()
        {
            var hasConn = SelectedConnection != null;
            var hasPair = SelectedPair != null;
            var hasSel = ChangesetGrid.SelectedItems.Count > 0;
            var loaded = _loadedConnection != null;

            EditConnectionButton.IsEnabled = hasConn && !_busy;
            DeleteConnectionButton.IsEnabled = hasConn && !_busy;
            AddPairButton.IsEnabled = hasConn && !_busy;
            EditPairButton.IsEnabled = hasPair && !_busy;
            DeletePairButton.IsEnabled = hasPair && !_busy;
            LoadButton.IsEnabled = hasPair && !_busy;
            ReloadButton.IsEnabled = loaded && !_busy;
            CopyButton.IsEnabled = hasSel;
            DetailsButton.IsEnabled = hasSel && loaded && !_busy;
            MergeButton.IsEnabled = hasSel && loaded && !_busy;
        }

        private void UpdateCount()
        {
            var visible = _view.Cast<object>().Count();
            var sel = ChangesetGrid.SelectedItems.Count;
            CountText.Text = $"{visible} von {_rows.Count} Changesets" + (sel > 0 ? $"  |  {sel} markiert" : "");
        }

        private void SetStatus(string text) => StatusText.Text = text;

        private void ShowError(string title, Exception ex)
        {
            var inner = ex;
            var sb = new StringBuilder();
            while (inner != null)
            {
                sb.AppendLine(inner.Message);
                inner = inner.InnerException;
            }
            MessageBox.Show(this, sb.ToString().Trim(), title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + " …";

        protected override void OnClosed(EventArgs e)
        {
            foreach (var id in _sessions.Keys.ToList()) DropSession(id);
            base.OnClosed(e);
        }
    }
}
