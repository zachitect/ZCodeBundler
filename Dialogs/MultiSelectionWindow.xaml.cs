using System.Windows;

namespace ZCodeBundler.Dialogs;

internal partial class MultiSelectionWindow : Window
{
    internal List<int> SelectedIndices { get; } = new();

    internal MultiSelectionWindow(string title, string instruction, List<string> items)
    {
        InitializeComponent();
        Title = title;
        TextTitle.Text = title;
        TextInstruction.Text = instruction;
        MainListBox.ItemsSource = items;
    }

    private void ButtonProceed_Click(object sender, RoutedEventArgs e)
    {
        SelectedIndices.Clear();

        if (MainListBox.SelectedItems.Count == 0)
        {
            new MessageDialog("Selection Required", "Please select at least one item before proceeding.", "OK", MessageBoxImage.Warning)
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        var selectedItems = new HashSet<object>(MainListBox.SelectedItems.Cast<object>());
        for (var index = 0; index < MainListBox.Items.Count; index++)
        {
            if (selectedItems.Contains(MainListBox.Items[index]))
                SelectedIndices.Add(index);
        }

        DialogResult = true;
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
    {
        MainListBox.SelectAll();
    }

    private void ButtonSelectNone_Click(object sender, RoutedEventArgs e)
    {
        MainListBox.SelectedItems.Clear();
    }
}