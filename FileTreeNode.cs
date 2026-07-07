namespace ZCodeBundler;

public sealed class FileTreeNode
{
    public FileTreeNode(
        string displayName,
        string fullPath,
        string relativePath,
        bool isFile,
        string fileType,
        bool isKnownFileType,
        DateTime? dateModified,
        List<FileTreeNode> children)
    {
        DisplayName = displayName;
        FullPath = fullPath;
        RelativePath = relativePath;
        IsFile = isFile;
        FileType = fileType;
        IsKnownFileType = isKnownFileType;
        DateModified = dateModified;
        Children = children;
    }

    public string DisplayName { get; }
    public string FullPath { get; }
    public string RelativePath { get; }
    public bool IsFile { get; }
    public string FileType { get; }
    public bool IsKnownFileType { get; }
    public DateTime? DateModified { get; }
    public List<FileTreeNode> Children { get; }
}
