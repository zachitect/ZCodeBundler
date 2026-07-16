using System.Collections.ObjectModel;

namespace ZCodeBundler.Dialogs.TreeSelection;

internal sealed class TreeSelectionViewModel
{
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

    internal void LoadFromNestedLists(IEnumerable<object> nestedItems, IEnumerable<object>? displayNames)
    {
        RootNodes.Clear();
        var items = nestedItems.ToArray();
        var names = displayNames?.ToArray();

        for (var index = 0; index < items.Length; index++)
        {
            var displayName = index < (names?.Length ?? 0) ? names![index] : null;
            var node = BuildNode(items[index], displayName);
            RootNodes.Add(node);
            SetParent(node, null);
        }
    }

    private static TreeNodeViewModel BuildNode(object item, object? displayNameStructure)
    {
        if (item is IEnumerable<object> nestedItems)
        {
            var groupName = "Group";
            object[] childNames = [];

            if (displayNameStructure is IEnumerable<object> nestedNames)
            {
                var names = nestedNames.ToArray();
                if (names.Length > 0 && names[0] is string name)
                {
                    groupName = name;
                    childNames = names.Skip(1).ToArray();
                }
            }
            else if (displayNameStructure is string directName)
            {
                groupName = directName;
            }

            var node = new TreeNodeViewModel { DisplayName = groupName };
            node.IsSelected = false;
            var items = nestedItems.ToArray();
            for (var index = 0; index < items.Length; index++)
            {
                var childName = index < childNames.Length ? childNames[index] : null;
                node.Children.Add(BuildNode(items[index], childName));
            }
            return node;
        }

        var itemName = displayNameStructure as string ?? item?.ToString() ?? "Item";
        var leaf = new TreeNodeViewModel { DisplayName = itemName };
        leaf.IsSelected = false;
        return leaf;
    }

    private static void SetParent(TreeNodeViewModel node, TreeNodeViewModel? parent)
    {
        node.Parent = parent;
        foreach (var child in node.Children)
            SetParent(child, node);
    }
}