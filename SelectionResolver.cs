namespace ZCodeBundler;

public sealed class SelectionResolver
{
    public List<FileTreeNode> ResolveSelectedFiles(FileTreeNode rootNode, List<List<int>> selectedIndices)
    {
        var selectedFiles = new Dictionary<string, FileTreeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var indexPath in selectedIndices)
        {
            var selectedNode = GetNodeByIndexPath(rootNode, indexPath);
            AddFilesFrom(selectedNode);
        }

        var files = selectedFiles.Values.ToList();
        files.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase));

        return files;

        void AddFilesFrom(FileTreeNode node)
        {
            if (node.IsFile)
            {
                selectedFiles.TryAdd(node.FullPath, node);
                return;
            }

            foreach (var child in node.Children)
                AddFilesFrom(child);
        }
    }

    private static FileTreeNode GetNodeByIndexPath(FileTreeNode rootNode, List<int> indexPath)
    {
        var currentNode = rootNode;

        foreach (var index in indexPath)
            currentNode = currentNode.Children[index];

        return currentNode;
    }
}