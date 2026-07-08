namespace ZCodeBundler.Decoding;

public sealed class DecodedBundleListItem
{
    public DecodedBundleListItem(string bundlePath, int decodedItemIndex, BundledCodeFile file)
    {
        BundlePath = bundlePath;
        DecodedItemIndex = decodedItemIndex;
        FileName = file.FileName;
        Path = file.Path;
        FileType = file.FileType;
        DateModified = file.DateModified;
        Content = file.Content;
        Status = DecodedSourceStatus.InvalidPath;
        StatusDetail = string.Empty;
    }

    public string BundlePath { get; }
    public int DecodedItemIndex { get; }
    public string FileName { get; }
    public string Path { get; }
    public string FileType { get; }
    public DateTime? DateModified { get; }
    public string Content { get; }
    public DecodedSourceStatus Status { get; set; }
    public string StatusDetail { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Path) ? FileName : Path;

    public string SourceFileName => string.IsNullOrWhiteSpace(FileName) ? DisplayName : FileName;

    public string BundleFileName
    {
        get
        {
            var fileName = System.IO.Path.GetFileName(BundlePath);
            return string.IsNullOrWhiteSpace(fileName) ? BundlePath : fileName;
        }
    }

    public string DateModifiedText => DateModified.HasValue
        ? DateModified.Value.ToString("yyyy-MM-dd HH:mm:ss")
        : "";

    public string StatusText => Status.ToString();
}