using System.Collections.Generic;
using System.Windows;

namespace MergeHelper
{
    public partial class ChangesetDetailsWindow : Window
    {
        public ChangesetDetailsWindow(ChangesetRow info, List<ChangeRow> changes)
        {
            InitializeComponent();
            Title = $"Changeset {info.Id}";
            UserText.Text = info.User;
            DateText.Text = info.DateText;
            CommentBox.Text = info.Comment;
            CountText.Text = $"{changes.Count} geänderte Datei(en):";
            ChangesGrid.ItemsSource = changes;
        }
    }
}
