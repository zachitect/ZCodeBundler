using System.IO;

namespace ZCodeBundler;

internal static class CodeFileTypeResolver
{
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


    public static string GetFileType(string fileNameOrPath)
    {
        var fileName = GetFileName(fileNameOrPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return "unknown";

        if (FileTypesByFileName.TryGetValue(fileName, out var fileType))
            return fileType;

        var extension = Path.GetExtension(fileName);
        if (FileTypesByExtension.TryGetValue(extension, out fileType))
            return fileType;

        return "unknown";
    }

    private static string GetFileName(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
            return string.Empty;

        var normalizedPath = fileNameOrPath.Trim().Replace('\\', '/').TrimEnd('/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        return lastSeparatorIndex < 0 ? normalizedPath : normalizedPath[(lastSeparatorIndex + 1)..];
    }
}
