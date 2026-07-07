namespace ZCodeBundler;

public sealed class BundleWriteResult
{
    public BundleWriteResult(string outputPath, int writtenFileCount, List<string> warnings)
    {
        OutputPath = outputPath;
        WrittenFileCount = writtenFileCount;
        Warnings = warnings;
    }

    public string OutputPath { get; }
    public int WrittenFileCount { get; }
    public List<string> Warnings { get; }
}