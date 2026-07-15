using System.Windows;

namespace ZCodeBundler.Dialogs.TreeSelection;

internal partial class TreeSelectionWindow : Window
{
    private readonly TreeSelectionViewModel _viewModel = new();

    internal List<List<int>> SelectedIndices { get; } = new();

    internal TreeSelectionWindow(string title, string instruction, List<object> nestedItems, List<object> displayNames)
    {
        InitializeComponent();
        Title = title;
        TextTitle.Text = title;
        TextInstruction.Text = instruction;
        DataContext = _viewModel;
        _viewModel.LoadFromNestedLists(nestedItems, displayNames);
    }

    private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var node in _viewModel.RootNodes)
            SetAllNodes(node, true);
    }

    private void ButtonSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var node in _viewModel.RootNodes)
            SetAllNodes(node, false);
    }

    private void ButtonExpandAll_Click(object sender, RoutedEventArgs e)
    {
        ExpandCollapseAll(_viewModel.RootNodes, true);
    }

    private void ButtonCollapseAll_Click(object sender, RoutedEventArgs e)
    {
        ExpandCollapseAll(_viewModel.RootNodes, false);
    }

    private static void ExpandCollapseAll(IEnumerable<TreeNodeViewModel> nodes, bool expand)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expand;
            ExpandCollapseAll(node.Children, expand);
        }
    }

    private static void SetAllNodes(TreeNodeViewModel node, bool selected)
    {
        node.SetIsSelected(selected, false, false);
        foreach (var child in node.Children)
            SetAllNodes(child, selected);
    }

    private void ButtonProceed_Click(object sender, RoutedEventArgs e)
    {
        SelectedIndices.Clear();

        void CollectSelected(TreeNodeViewModel node, List<int> path)
        {
            if (node.IsSelected == true)
                SelectedIndices.Add(new List<int>(path));

            for (var index = 0; index < node.Children.Count; index++)
                CollectSelected(node.Children[index], new List<int>(path) { index });
        }

        for (var index = 0; index < _viewModel.RootNodes.Count; index++)
            CollectSelected(_viewModel.RootNodes[index], new List<int> { index });

        if (SelectedIndices.Count == 0)
        {
            new ZCodeBundler.Dialogs.MessageDialog("Selection Required", "Please select at least one item before proceeding.", "OK", MessageBoxImage.Warning)
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        DialogResult = true;
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}