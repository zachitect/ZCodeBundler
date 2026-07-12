using System.IO;
using System.Text;

namespace ZCodeBundler.Bundling;

public sealed class FileTreeScanner
{
    private static readonly HashSet<string> SkippedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        ".next",
        ".nuxt",
        ".svelte-kit",
        ".angular",
        ".pytest_cache",
        ".mypy_cache",
        ".ruff_cache",
        ".tox",
        ".venv",
        "venv",
        "env",
        "__pycache__",
        "bin",
        "obj",
        "node_modules",
        "bower_components",
        "packages",
        "TestResults",
        "coverage",
        "dist",
        "build",
        "out",
        "target",
        ".gradle",
        ".terraform"
    };

    private static readonly HashSet<string> SkippedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll",
        ".exe",
        ".pdb",
        ".cache",
        ".suo",
        ".user",
        ".nupkg",
        ".snupkg",
        ".zip",
        ".7z",
        ".rar",
        ".tar",
        ".gz",
        ".tgz",
        ".bz2",
        ".xz",
        ".iso",
        ".msi",
        ".dmg",
        ".pkg",
        ".deb",
        ".rpm",
        ".apk",
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif",
        ".bmp",
        ".ico",
        ".tif",
        ".tiff",
        ".psd",
        ".ai",
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
        ".mp3",
        ".mp4",
        ".mov",
        ".avi",
        ".mkv",
        ".webm",
        ".wav",
        ".flac",
        ".ogg",
        ".woff",
        ".woff2",
        ".ttf",
        ".otf",
        ".eot",
        ".db",
        ".sqlite",
        ".sqlite3",
        ".bak",
        ".tmp",
        ".temp",
        ".class",
        ".jar",
        ".war",
        ".ear",
        ".wasm",
        ".pyc",
        ".pyo",
        ".map"
    };

    public FileTreeNode Scan(string rootFolderPath)
    {
        if (string.IsNullOrWhiteSpace(rootFolderPath))
            throw new ArgumentException("Root folder path is required.", nameof(rootFolderPath));

        if (!Directory.Exists(rootFolderPath))
            throw new DirectoryNotFoundException($"Root folder does not exist: {rootFolderPath}");

        var rootDirectory = new DirectoryInfo(rootFolderPath);

        return new FileTreeNode(
            rootDirectory.Name,
            rootDirectory.FullName,
            string.Empty,
            isFile: false,
            fileType: string.Empty,
            isKnownFileType: true,
            dateModified: null,
            children: ScanChildren(rootDirectory, rootDirectory.FullName));
    }

    public FileTreeNode ScanMany(IReadOnlyList<string> rootFolderPaths)
    {
        if (rootFolderPaths.Count == 0)
            throw new ArgumentException("At least one root folder path is required.", nameof(rootFolderPaths));

        var children = new List<FileTreeNode>();
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rootFolderPath in rootFolderPaths)
        {
            var rootNode = Scan(rootFolderPath);
            var dedupedRootNode = RemoveDuplicateFiles(rootNode, seenFiles);

            if (dedupedRootNode.Children.Count > 0)
                children.Add(dedupedRootNode);
        }

        return new FileTreeNode(
            "Selected Roots",
            string.Empty,
            string.Empty,
            isFile: false,
            fileType: string.Empty,
            isKnownFileType: true,
            dateModified: null,
            children);
    }

    public FileTreeNode? CreateFileNode(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File does not exist: {filePath}", filePath);

        var file = new FileInfo(filePath);

        if (SkippedFileExtensions.Contains(file.Extension))
            return null;

        if (IsProbablyBinary(file))
            return null;

        var fileType = GetFileType(file);
        var fullPath = file.Directory == null
            ? file.FullName
            : Path.Combine(file.Directory.FullName, file.Name);
        var parentFolderName = file.Directory?.Name ?? string.Empty;

        var relativePath = string.IsNullOrWhiteSpace(parentFolderName)
            ? file.Name
            : Path.Combine(parentFolderName, file.Name).Replace('\\', '/');

        return new FileTreeNode(
            file.Name,
            fullPath,
            relativePath,
            isFile: true,
            fileType,
            isKnownFileType: fileType != "unknown",
            dateModified: file.LastWriteTime,
            children: new List<FileTreeNode>());
    }

    private static List<FileTreeNode> ScanChildren(DirectoryInfo directory, string rootFolderPath)
    {
        var children = new List<FileTreeNode>();

        foreach (var childDirectory in GetDirectories(directory))
        {
            if (SkippedFolderNames.Contains(childDirectory.Name)
                || childDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var childFullPath = Path.Combine(directory.FullName, childDirectory.Name);

            children.Add(new FileTreeNode(
                childDirectory.Name,
                childFullPath,
                GetRelativePath(rootFolderPath, childFullPath),
                isFile: false,
                fileType: string.Empty,
                isKnownFileType: true,
                dateModified: null,
                children: ScanChildren(childDirectory, rootFolderPath)));
        }

        foreach (var file in GetFiles(directory))
        {
            if (SkippedFileExtensions.Contains(file.Extension))
                continue;

            if (IsProbablyBinary(file))
                continue;

            var fileType = GetFileType(file);
            var isKnownFileType = fileType != "unknown";
            var fullPath = Path.Combine(directory.FullName, file.Name);

            children.Add(new FileTreeNode(
                file.Name,
                fullPath,
                GetRelativePath(rootFolderPath, fullPath),
                isFile: true,
                fileType,
                isKnownFileType,
                dateModified: file.LastWriteTime,
                children: new List<FileTreeNode>()));
        }

        return children;
    }

    private static FileTreeNode RemoveDuplicateFiles(FileTreeNode node, HashSet<string> seenFiles)
    {
        if (node.IsFile)
            return node;

        var children = new List<FileTreeNode>();

        foreach (var child in node.Children)
        {
            if (child.IsFile)
            {
                if (seenFiles.Add(child.FullPath))
                    children.Add(child);

                continue;
            }

            var dedupedFolder = RemoveDuplicateFiles(child, seenFiles);

            if (dedupedFolder.Children.Count > 0)
                children.Add(dedupedFolder);
        }

        return new FileTreeNode(
            node.DisplayName,
            node.FullPath,
            node.RelativePath,
            isFile: false,
            node.FileType,
            node.IsKnownFileType,
            node.DateModified,
            children);
    }

    private static DirectoryInfo[] GetDirectories(DirectoryInfo directory)
    {
        try
        {
            var directories = directory.GetDirectories();

            Array.Sort(directories, (left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

            return directories;
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<DirectoryInfo>();
        }
        catch (IOException)
        {
            return Array.Empty<DirectoryInfo>();
        }
    }

    private static FileInfo[] GetFiles(DirectoryInfo directory)
    {
        try
        {
            var files = directory.GetFiles();

            Array.Sort(files, (left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

            return files;
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<FileInfo>();
        }
        catch (IOException)
        {
            return Array.Empty<FileInfo>();
        }
    }

    private static bool IsProbablyBinary(FileInfo file)
    {
        var strictUtf8 = new UTF8Encoding(false, true);

        try
        {
            using var reader = new StreamReader(file.FullName, strictUtf8, false);
            var buffer = new char[4096];
            int charactersRead;

            while ((charactersRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var index = 0; index < charactersRead; index++)
                {
                    if (buffer[index] == '\0')
                        return true;
                }
            }

            return false;
        }
        catch (DecoderFallbackException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static string GetRelativePath(string rootFolderPath, string fullPath)
    {
        return Path.GetRelativePath(rootFolderPath, fullPath).Replace('\\', '/');
    }

    private static string GetFileType(FileInfo file)
    {
        return ZCodeBundler.CodeFileTypeResolver.GetFileType(file.Name);
    }
}