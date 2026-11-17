using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MergeHelper
{
    /// <summary>Einfaches Fenster zur Anzeige eines (kopierbaren) Textes, z.B. Merge-Protokoll.</summary>
    public class TextWindow : Window
    {
        public TextWindow(string title, string text)
        {
            Title = title;
            Width = 750;
            Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            var close = new Button { Content = "Schließen", IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Bottom);

            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true
            };

            var panel = new DockPanel { Margin = new Thickness(10) };
            panel.Children.Add(close);
            panel.Children.Add(box);
            Content = panel;
        }
    }
}
