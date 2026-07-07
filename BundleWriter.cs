using System.IO;
using System.Text;

namespace ZCodeBundler;

public sealed class BundleWriter
{
    private const string FileStartDelimiter = "--- ZCODEBUNDLE_FILE_START ---";
    private const string ContentStartDelimiter = "--- ZCODEBUNDLE_CONTENT_START ---";
    private const string FileEndDelimiter = "--- ZCODEBUNDLE_FILE_END ---";

    private static readonly Encoding OutputEncoding = new UTF8Encoding(false);

    public BundleWriteResult Write(string rootFolderPath, IReadOnlyList<FileTreeNode> selectedFiles, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(rootFolderPath))
            throw new ArgumentException("Root folder path is required.", nameof(rootFolderPath));

        if (selectedFiles.Count == 0)
            throw new InvalidOperationException("No files were selected.");

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path is required.", nameof(outputPath));

        var warnings = new List<string>();
        var temporaryBodyPath = Path.GetTempFileName();

        try
        {
            var writtenFileCount = WriteBundleBody(temporaryBodyPath, selectedFiles, warnings);

            if (writtenFileCount == 0)
                throw new InvalidOperationException("No selected files could be read.");

            WriteFinalBundle(rootFolderPath, outputPath, temporaryBodyPath, writtenFileCount);

            return new BundleWriteResult(outputPath, writtenFileCount, warnings);
        }
        finally
        {
            if (File.Exists(temporaryBodyPath))
                File.Delete(temporaryBodyPath);
        }
    }

    private static int WriteBundleBody(string bodyPath, IReadOnlyList<FileTreeNode> selectedFiles, List<string> warnings)
    {
        var writtenFileCount = 0;

        using var writer = new StreamWriter(bodyPath, false, OutputEncoding);

        foreach (var file in selectedFiles)
        {
            try
            {
                WriteFileBlock(writer, file);
                writtenFileCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not read file: {file.RelativePath}");
            }
        }

        return writtenFileCount;
    }

    private static void WriteFinalBundle(string rootFolderPath, string outputPath, string bodyPath, int writtenFileCount)
    {
        using var outputWriter = new StreamWriter(outputPath, false, OutputEncoding);

        outputWriter.WriteLine("ZCODEBUNDLE_VERSION: 1");
        outputWriter.WriteLine("BUNDLE_TITLE: Selected Code Bundle");
        outputWriter.WriteLine($"ROOT_PATH: {rootFolderPath}");
        outputWriter.WriteLine($"GENERATED_AT: {DateTime.Now:yyyy-MM-dd HH:mm}");
        outputWriter.WriteLine($"FILE_COUNT: {writtenFileCount}");
        outputWriter.WriteLine();
        WriteReaderInstructions(outputWriter);
        outputWriter.WriteLine();

        using var bodyReader = new StreamReader(bodyPath, OutputEncoding);

        var buffer = new char[8192];
        int charactersRead;

        while ((charactersRead = bodyReader.Read(buffer, 0, buffer.Length)) > 0)
            outputWriter.Write(buffer, 0, charactersRead);
    }

    private static void WriteReaderInstructions(StreamWriter writer)
    {
        writer.WriteLine("ZCODEBUNDLE_READER_INSTRUCTIONS:");
        writer.WriteLine("This file is a ZCodeBundle multi-file source bundle.");
        writer.WriteLine($"Each bundled file starts with {FileStartDelimiter}.");
        writer.WriteLine($"File metadata appears before {ContentStartDelimiter}.");
        writer.WriteLine($"Original file content appears after {ContentStartDelimiter}.");
        writer.WriteLine($"Each bundled file ends at {FileEndDelimiter}.");
        writer.WriteLine("Use PATH as the file identity.");
        writer.WriteLine("Do not treat bundle headers, metadata lines, or delimiter lines as source code.");
    }

    private static void WriteFileBlock(StreamWriter writer, FileTreeNode file)
    {
        var modifiedText = file.DateModified.HasValue
            ? file.DateModified.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : string.Empty;

        writer.WriteLine(FileStartDelimiter);
        writer.WriteLine($"FILE_NAME: {file.DisplayName}");
        writer.WriteLine($"PATH: {file.RelativePath}");
        writer.WriteLine($"TYPE: {file.FileType}");
        writer.WriteLine($"DATE_MODIFIED: {modifiedText}");
        writer.WriteLine(ContentStartDelimiter);

        using var reader = new StreamReader(file.FullPath, Encoding.UTF8, true);

        var buffer = new char[8192];
        int charactersRead;

        while ((charactersRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            writer.Write(buffer, 0, charactersRead);

        writer.WriteLine();
        writer.WriteLine(FileEndDelimiter);
    }
}