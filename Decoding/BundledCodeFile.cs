namespace ZCodeBundler.Decoding;

public sealed class BundledCodeFile
{
    public BundledCodeFile(
        string fileName,
        string path,
        string fileType,
        DateTime? dateModified,
        string content)
    {
        FileName = fileName;
        Path = path;
        FileType = fileType;
        DateModified = dateModified;
        Content = content;
    }

    public string FileName { get; }
    public string Path { get; }
    public string FileType { get; }
    public DateTime? DateModified { get; }
    public string Content { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Path) ? FileName : Path;
}