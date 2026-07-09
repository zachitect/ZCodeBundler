using System.Globalization;
using System.IO;

namespace ZCodeBundler.Decoding;

public sealed class ZCodeBundleReader
{
    private const string FileStartDelimiter = "--- ZCODEBUNDLE_" + "FILE_START ---";
    private const string ContentStartDelimiter = "--- ZCODEBUNDLE_" + "CONTENT_START ---";
    private const string FileEndDelimiter = "--- ZCODEBUNDLE_" + "FILE_END ---";

    public List<BundledCodeFile> Read(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
            throw new ArgumentException("Bundle path is required.", nameof(bundlePath));

        var text = File.ReadAllText(bundlePath);
        return ReadFromText(text);
    }

    public List<BundledCodeFile> ReadFromText(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        var files = new List<BundledCodeFile>();
        var searchIndex = 0;

        while (true)
        {
            var fileStartIndex = FindNextDelimiterLine(text, FileStartDelimiter, searchIndex);
            if (fileStartIndex < 0)
                break;

            var metadataStartIndex = fileStartIndex + FileStartDelimiter.Length;
            var contentStartIndex = FindNextDelimiterLine(text, ContentStartDelimiter, metadataStartIndex);

            if (contentStartIndex < 0)
                throw new InvalidDataException("Bundle file is missing a content start delimiter.");

            var metadataText = text[metadataStartIndex..contentStartIndex].Trim();
            var metadata = ParseMetadata(metadataText);

            metadata.TryGetValue("FILE_NAME", out var fileName);
            metadata.TryGetValue("PATH", out var path);
            metadata.TryGetValue("TYPE", out var fileType);
            metadata.TryGetValue("DATE_MODIFIED", out var dateModifiedText);

            var contentBodyIndex = MovePastSingleNewLine(text, contentStartIndex + ContentStartDelimiter.Length);

            string content;
            int fileEndIndex;

            if (metadata.TryGetValue("CONTENT_LENGTH", out var contentLengthText))
            {
                var contentLength = ParseContentLength(contentLengthText);
                var contentEndIndex = contentBodyIndex + contentLength;

                if (contentEndIndex > text.Length)
                    throw new InvalidDataException("Bundle file content length exceeds the remaining bundle text.");

                content = text.Substring(contentBodyIndex, contentLength);

                fileEndIndex = FindNextDelimiterLine(text, FileEndDelimiter, contentEndIndex);
                if (fileEndIndex < 0)
                    throw new InvalidDataException("Bundle file is missing a file end delimiter.");
            }
            else
            {
                fileEndIndex = FindNextDelimiterLine(text, FileEndDelimiter, contentBodyIndex);
                if (fileEndIndex < 0)
                    throw new InvalidDataException("Bundle file is missing a file end delimiter.");

                content = text[contentBodyIndex..fileEndIndex];
                content = TrimSingleTrailingNewLine(content);
            }

            var normalizedPath = path ?? string.Empty;
            var normalizedFileName = NormalizeFileName(fileName, normalizedPath);
            var normalizedFileType = NormalizeFileType(fileType, normalizedFileName, normalizedPath);

            files.Add(new BundledCodeFile(
                normalizedFileName,
                normalizedPath,
                normalizedFileType,
                ParseDateModified(dateModifiedText),
                content));

            searchIndex = fileEndIndex + FileEndDelimiter.Length;
        }

        return files;
    }


    private static int FindNextDelimiterLine(string text, string delimiter, int startIndex)
    {
        var searchIndex = startIndex;

        while (searchIndex < text.Length)
        {
            var delimiterIndex = text.IndexOf(delimiter, searchIndex, StringComparison.Ordinal);

            if (delimiterIndex < 0)
                return -1;

            if (IsStandaloneDelimiterLine(text, delimiter, delimiterIndex))
                return delimiterIndex;

            searchIndex = delimiterIndex + delimiter.Length;
        }

        return -1;
    }

    private static bool IsStandaloneDelimiterLine(string text, string delimiter, int delimiterIndex)
    {
        var isAtLineStart = delimiterIndex == 0
            || text[delimiterIndex - 1] == '\n'
            || text[delimiterIndex - 1] == '\r';

        if (!isAtLineStart)
            return false;

        var afterDelimiterIndex = delimiterIndex + delimiter.Length;

        return afterDelimiterIndex == text.Length
            || text[afterDelimiterIndex] == '\r'
            || text[afterDelimiterIndex] == '\n';
    }

    private static Dictionary<string, string> ParseMetadata(string metadataText)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(metadataText);

        while (reader.ReadLine() is { } line)
        {
            var separatorIndex = line.IndexOf(':');

            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            metadata[key] = value;
        }

        return metadata;
    }

    private static int ParseContentLength(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var contentLength))
            throw new InvalidDataException("Bundle file has an invalid content length.");

        if (contentLength < 0)
            throw new InvalidDataException("Bundle file has a negative content length.");

        return contentLength;
    }

    private static string NormalizeFileName(string? fileName, string path)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
            return fileName;

        return GetFinalPathSegment(path);
    }

    private static string NormalizeFileType(string? fileType, string fileName, string path)
    {
        if (!string.IsNullOrWhiteSpace(fileType))
            return fileType;

        var fileTypeSource = string.IsNullOrWhiteSpace(fileName) ? path : fileName;
        return ZCodeBundler.CodeFileTypeResolver.GetFileType(fileTypeSource);
    }

    private static string GetFinalPathSegment(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var normalizedPath = path.Trim().Replace('\\', '/').TrimEnd('/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        return lastSeparatorIndex < 0 ? normalizedPath : normalizedPath[(lastSeparatorIndex + 1)..];
    }

    private static DateTime? ParseDateModified(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dateModified))
        {
            return dateModified;
        }

        return null;
    }

    private static int MovePastSingleNewLine(string text, int index)
    {
        if (index >= text.Length)
            return index;

        if (text[index] == '\r')
        {
            if (index + 1 < text.Length && text[index + 1] == '\n')
                return index + 2;

            return index + 1;
        }

        if (text[index] == '\n')
            return index + 1;

        return index;
    }

    private static string TrimSingleTrailingNewLine(string value)
    {
        if (value.EndsWith("\r\n", StringComparison.Ordinal))
            return value[..^2];

        if (value.EndsWith('\n'))
            return value[..^1];

        if (value.EndsWith('\r'))
            return value[..^1];

        return value;
    }
}
