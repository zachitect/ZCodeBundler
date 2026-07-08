using System.IO;

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

    private static readonly Dictionary<string, string> FileTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".csx"] = "csharp-script",
        [".csproj"] = "xml-project",
        [".fs"] = "fsharp",
        [".fsx"] = "fsharp-script",
        [".fsproj"] = "xml-project",
        [".vb"] = "visual-basic",
        [".vbproj"] = "xml-project",
        [".sln"] = "solution",
        [".slnx"] = "solution",
        [".slnf"] = "solution-filter",
        [".props"] = "xml-msbuild",
        [".targets"] = "xml-msbuild",
        [".resx"] = "xml-resource",
        [".js"] = "javascript",
        [".jsx"] = "javascript-react",
        [".ts"] = "typescript",
        [".tsx"] = "typescript-react",
        [".mjs"] = "javascript-module",
        [".cjs"] = "javascript-commonjs",
        [".vue"] = "vue",
        [".svelte"] = "svelte",
        [".astro"] = "astro",
        [".html"] = "html",
        [".htm"] = "html",
        [".css"] = "css",
        [".scss"] = "scss",
        [".sass"] = "sass",
        [".less"] = "less",
        [".postcss"] = "postcss",
        [".json"] = "json",
        [".jsonc"] = "json-with-comments",
        [".xml"] = "xml",
        [".xaml"] = "xaml",
        [".svg"] = "svg",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".toml"] = "toml",
        [".ini"] = "ini",
        [".cfg"] = "config",
        [".conf"] = "config",
        [".config"] = "config",
        [".properties"] = "properties",
        [".env"] = "env",
        [".md"] = "markdown",
        [".markdown"] = "markdown",
        [".mdx"] = "markdown-jsx",
        [".txt"] = "text",
        [".log"] = "log",
        [".csv"] = "csv",
        [".tsv"] = "tsv",
        [".sql"] = "sql",
        [".graphql"] = "graphql",
        [".gql"] = "graphql",
        [".proto"] = "protobuf",
        [".py"] = "python",
        [".pyw"] = "python",
        [".ipynb"] = "jupyter-notebook",
        [".java"] = "java",
        [".kt"] = "kotlin",
        [".kts"] = "kotlin-script",
        [".scala"] = "scala",
        [".groovy"] = "groovy",
        [".gradle"] = "gradle",
        [".go"] = "go",
        [".rs"] = "rust",
        [".zig"] = "zig",
        [".cpp"] = "cpp",
        [".cc"] = "cpp",
        [".cxx"] = "cpp",
        [".c"] = "c",
        [".h"] = "c-header",
        [".hh"] = "cpp-header",
        [".hpp"] = "cpp-header",
        [".hxx"] = "cpp-header",
        [".m"] = "objective-c",
        [".mm"] = "objective-cpp",
        [".php"] = "php",
        [".rb"] = "ruby",
        [".swift"] = "swift",
        [".dart"] = "dart",
        [".lua"] = "lua",
        [".r"] = "r",
        [".jl"] = "julia",
        [".ex"] = "elixir",
        [".exs"] = "elixir-script",
        [".erl"] = "erlang",
        [".hrl"] = "erlang-header",
        [".sh"] = "shell",
        [".bash"] = "bash",
        [".zsh"] = "zsh",
        [".fish"] = "fish",
        [".ps1"] = "powershell",
        [".psm1"] = "powershell-module",
        [".psd1"] = "powershell-data",
        [".bat"] = "batch",
        [".cmd"] = "batch",
        [".dockerfile"] = "dockerfile",
        [".tf"] = "terraform",
        [".tfvars"] = "terraform-vars",
        [".hcl"] = "hcl",
        [".razor"] = "razor",
        [".cshtml"] = "razor-view",
        [".vbhtml"] = "razor-view",
        [".feature"] = "gherkin",
        [".http"] = "http-request",
        [".rest"] = "http-request",
        [".editorconfig"] = "editorconfig",
        [".gitignore"] = "gitignore",
        [".gitattributes"] = "gitattributes",
        [".dockerignore"] = "dockerignore",
        [".npmignore"] = "npmignore"
    };

    private static readonly Dictionary<string, string> FileTypesByFileName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dockerfile"] = "dockerfile",
        ["Containerfile"] = "containerfile",
        ["Makefile"] = "makefile",
        ["CMakeLists.txt"] = "cmake",
        ["Directory.Build.props"] = "xml-msbuild",
        ["Directory.Build.targets"] = "xml-msbuild",
        ["Directory.Packages.props"] = "xml-msbuild",
        ["global.json"] = "dotnet-global-json",
        ["appsettings.json"] = "dotnet-appsettings",
        ["appsettings.Development.json"] = "dotnet-appsettings",
        ["launchSettings.json"] = "dotnet-launch-settings",
        ["package.json"] = "npm-package",
        ["package-lock.json"] = "npm-lock",
        ["pnpm-lock.yaml"] = "pnpm-lock",
        ["yarn.lock"] = "yarn-lock",
        ["bun.lockb"] = "bun-lock",
        ["tsconfig.json"] = "typescript-config",
        ["jsconfig.json"] = "javascript-config",
        ["vite.config.js"] = "vite-config",
        ["vite.config.ts"] = "vite-config",
        ["webpack.config.js"] = "webpack-config",
        ["rollup.config.js"] = "rollup-config",
        ["eslint.config.js"] = "eslint-config",
        ["eslint.config.mjs"] = "eslint-config",
        ["prettier.config.js"] = "prettier-config",
        [".prettierrc"] = "prettier-config",
        [".prettierrc.json"] = "prettier-config",
        [".eslintrc"] = "eslint-config",
        [".eslintrc.json"] = "eslint-config",
        [".eslintrc.js"] = "eslint-config",
        ["pyproject.toml"] = "python-project",
        ["requirements.txt"] = "python-requirements",
        ["Pipfile"] = "python-pipfile",
        ["Pipfile.lock"] = "python-lock",
        ["poetry.lock"] = "python-lock",
        ["Gemfile"] = "ruby-gemfile",
        ["Gemfile.lock"] = "ruby-lock",
        ["go.mod"] = "go-module",
        ["go.sum"] = "go-checksums",
        ["Cargo.toml"] = "rust-package",
        ["Cargo.lock"] = "rust-lock",
        ["composer.json"] = "php-composer",
        ["composer.lock"] = "php-lock",
        [".env"] = "env",
        [".env.local"] = "env",
        [".env.development"] = "env",
        [".env.production"] = "env",
        [".env.test"] = "env",
        [".editorconfig"] = "editorconfig",
        [".gitignore"] = "gitignore",
        [".gitattributes"] = "gitattributes",
        [".dockerignore"] = "dockerignore",
        [".npmignore"] = "npmignore",
        ["README"] = "readme",
        ["README.md"] = "markdown",
        ["LICENSE"] = "license",
        ["LICENSE.md"] = "license",
        ["CHANGELOG"] = "changelog",
        ["CHANGELOG.md"] = "markdown"
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
            if (SkippedFolderNames.Contains(childDirectory.Name))
                continue;

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
        const int sampleSize = 4096;

        try
        {
            using var stream = file.OpenRead();

            var length = (int)Math.Min(sampleSize, stream.Length);
            var buffer = new byte[length];
            var bytesRead = stream.Read(buffer, 0, length);

            for (var i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                    return true;
            }

            return false;
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
        if (FileTypesByFileName.TryGetValue(file.Name, out var fileType))
            return fileType;

        if (FileTypesByExtension.TryGetValue(file.Extension, out fileType))
            return fileType;

        return "unknown";
    }
}