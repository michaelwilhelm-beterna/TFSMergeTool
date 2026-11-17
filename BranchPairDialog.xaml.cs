using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MergeHelper
{
    public partial class BranchPairDialog : Window
    {
        private readonly List<BranchInfo> _branches;
        public BranchPair Result { get; private set; }

        public BranchPairDialog(List<BranchInfo> branches, BranchPair existing)
        {
            InitializeComponent();
            _branches = branches;
            SourceBox.ItemsSource = _branches.Select(b => b.Path).ToList();
            Title = existing == null ? "Neues Branch-Paar" : "Branch-Paar bearbeiten";

            if (existing != null)
            {
                SourceBox.Text = existing.Source;
                FillTargets();
                if (!TargetBox.Items.Cast<string>().Any(t => Eq(t, existing.Target)))
                    AllTargetsCheck.IsChecked = true;
                TargetBox.Text = existing.Target;
            }
            Loaded += (s, e) => SourceBox.Focus();
        }

        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private BranchInfo Find(string path) => _branches.FirstOrDefault(b => Eq(b.Path, path));

        private void SourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var oldTarget = TargetBox.Text;
            // SelectionChanged feuert bevor Text aktualisiert ist -> SelectedItem verwenden
            FillTargets(SourceBox.SelectedItem as string);
            if (TargetBox.Items.Cast<string>().Any(t => Eq(t, oldTarget))) TargetBox.Text = oldTarget;
            else if (TargetBox.Items.Count == 1) TargetBox.SelectedIndex = 0;
            else TargetBox.Text = "";
        }

        private void TargetBox_DropDownOpened(object sender, EventArgs e)
        {
            // Quelle evtl. von Hand eingetippt -> Zielliste aktualisieren
            var t = TargetBox.Text;
            FillTargets();
            TargetBox.Text = t;
        }

        private void AllTargets_Changed(object sender, RoutedEventArgs e)
        {
            var t = TargetBox.Text;
            FillTargets();
            TargetBox.Text = t;
        }

        private void FillTargets(string source = null)
        {
            source = source ?? SourceBox.Text;
            IEnumerable<string> targets;
            var src = Find(source);
            if (AllTargetsCheck.IsChecked == true || src == null)
            {
                targets = _branches.Select(b => b.Path);
            }
            else
            {
                var related = new List<string>();
                if (!string.IsNullOrEmpty(src.Parent)) related.Add(src.Parent);
                related.AddRange(src.Children);
                // Auch Branches, die die Quelle als Parent haben (falls ChildBranches unvollständig)
                related.AddRange(_branches.Where(b => Eq(b.Parent, src.Path)).Select(b => b.Path));
                targets = related;
            }
            TargetBox.ItemsSource = targets.Where(t => !Eq(t, source))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void Swap_Click(object sender, RoutedEventArgs e)
        {
            var s = SourceBox.Text;
            var t = TargetBox.Text;
            SourceBox.Text = t;
            FillTargets(t);
            if (!TargetBox.Items.Cast<string>().Any(x => Eq(x, s))) AllTargetsCheck.IsChecked = true;
            TargetBox.Text = s;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var s = SourceBox.Text.Trim();
            var t = TargetBox.Text.Trim();
            if (!s.StartsWith("$/") || !t.StartsWith("$/"))
            {
                ErrorText.Text = "Bitte Quelle und Ziel auswählen (Serverpfade beginnen mit $/).";
                return;
            }
            if (Eq(s, t))
            {
                ErrorText.Text = "Quelle und Ziel dürfen nicht gleich sein.";
                return;
            }
            Result = new BranchPair { Source = s, Target = t };
            DialogResult = true;
        }
    }
}
