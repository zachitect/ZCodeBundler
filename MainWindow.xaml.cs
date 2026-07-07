using DataGridPreview.Core.APIReferences;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static DataGridPreview.Core.APIReferences.APIOfDialogs;

namespace ZCodeBundler
{
    public partial class MainWindow : Window
    {
        private readonly List<FileTreeNode> _selectedFiles = new();
        private readonly DispatcherTimer _statusMessageTimer;

        public MainWindow()
        {
            InitializeComponent();

            _statusMessageTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _statusMessageTimer.Tick += StatusMessageTimer_Tick;

            RefreshSelectedFilesList();
            UpdateFileActionButtons();
            UpdateTotalFilesStatus();
        }

        private void SelectRootFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var githubFolder = Path.Combine(documentsFolder, "GitHub");

            var dialog = new OpenFolderDialog
            {
                Title = "Select a folder to add",
                InitialDirectory = Directory.Exists(githubFolder) ? githubFolder : documentsFolder
            };

            if (dialog.ShowDialog(this) != true)
                return;

            AddFolders(new List<string> { dialog.FolderName });
        }

        private void CreateBundleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0)
                return;

            var downloadsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            var commonRootPath = GetCommonRootDirectory(_selectedFiles);
            var bundleNamePrefix = GetBundleNamePrefix(commonRootPath);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var defaultFileName = $"{bundleNamePrefix}-{timestamp}.zcb.txt";

            var dialog = new SaveFileDialog
            {
                Title = "Save ZCodeBundle",
                FileName = defaultFileName,
                InitialDirectory = Directory.Exists(downloadsFolder)
                    ? downloadsFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                DefaultExt = ".txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                var rootPath = commonRootPath ?? "Multiple roots / direct files";
                var writer = new BundleWriter();
                var result = writer.Write(rootPath, _selectedFiles, dialog.FileName);

                var message = $"Bundle created:\n{result.OutputPath}\n\nFiles written: {result.WrittenFileCount}";

                if (result.Warnings.Count > 0)
                    message += $"\n\nWarnings: {result.Warnings.Count}\n{string.Join("\n", result.Warnings)}";

                new APIOfDialogs.DialogMsgBoxAC(
                    $"Bundle created",
                    message,
                    "OK",
                    result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning).ShowDialog();

                ShowTemporaryStatus($"Bundle created. Files written: {result.WrittenFileCount}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                new APIOfDialogs.DialogMsgBoxAC(
                    $"Could not create bundle",
                    ex.Message,
                    "OK",
                    MessageBoxImage.Error).ShowDialog();

                ShowTemporaryStatus("Could not create bundle.");
            }
        }

        private void RemoveSelectedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFilesListView.SelectedItems.Count == 0)
                return;

            var selectedFiles = new List<FileTreeNode>();

            foreach (var item in SelectedFilesListView.SelectedItems)
            {
                if (item is FileTreeNode file)
                    selectedFiles.Add(file);
            }

            if (selectedFiles.Count == 0)
                return;

            var selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in selectedFiles)
                selectedPaths.Add(file.FullPath);

            _selectedFiles.RemoveAll(file => selectedPaths.Contains(file.FullPath));

            RefreshSelectedFilesList();
            UpdateFileActionButtons();
            UpdateTotalFilesStatus();
            ShowTemporaryStatus($"Removed {selectedFiles.Count} files.");
        }

        private void ClearAllFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0)
                return;

            var removedCount = _selectedFiles.Count;

            _selectedFiles.Clear();

            RefreshSelectedFilesList();
            UpdateFileActionButtons();
            UpdateTotalFilesStatus();
            ShowTemporaryStatus($"Cleared {removedCount} files.");
        }

        private void SelectedFilesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateFileActionButtons();
        }

        private void ListDropGridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectedFilesAreaRow.Height = new GridLength(1, GridUnitType.Star);
            DropAreaRow.Height = new GridLength(1, GridUnitType.Star);
        }

        private void FolderDropArea_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = HasDroppedFilesOrFolders(e) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void FolderDropArea_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = HasDroppedFilesOrFolders(e) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void FolderDropArea_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            var folderPaths = new List<string>();
            var filePaths = new List<string>();

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    folderPaths.Add(path);
                    continue;
                }

                if (File.Exists(path))
                    filePaths.Add(path);
            }

            if (folderPaths.Count > 0)
                AddFolders(folderPaths);

            if (filePaths.Count > 0)
                AddFiles(filePaths);

            if (folderPaths.Count == 0 && filePaths.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Drop folders or files.",
                    "Invalid drop",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ShowTemporaryStatus("Invalid drop.");
            }
        }

        private void AddFolders(IReadOnlyList<string> folderPaths)
        {
            try
            {
                var scanner = new FileTreeScanner();
                var scannedRootNode = scanner.ScanMany(folderPaths);
                var filteredRootNode = FilterUnknownFileTypes(scannedRootNode);
                var fileCount = CountFiles(filteredRootNode);

                if (fileCount == 0)
                {
                    MessageBox.Show(
                        this,
                        "No eligible files were found in the selected folder.",
                        "No files found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    ShowTemporaryStatus("No eligible files found.");
                    return;
                }

                if (UseTreeSelectionCheckBox.IsChecked == true)
                {
                    ShowTreeSelectionDialog(filteredRootNode, folderPaths);
                    return;
                }

                var files = GetAllFiles(filteredRootNode);
                var filesWithBundlePaths = ApplyMultipleRootPrefixIfNeeded(files, folderPaths);

                AddSelectedFiles(filesWithBundlePaths);
            }
            catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Could not scan folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ShowTemporaryStatus("Could not scan folder.");
            }
        }

        private void AddFiles(IReadOnlyList<string> filePaths)
        {
            var scanner = new FileTreeScanner();
            var files = new List<FileTreeNode>();

            foreach (var filePath in filePaths)
            {
                try
                {
                    var fileNode = scanner.CreateFileNode(filePath);

                    if (fileNode != null)
                        files.Add(fileNode);
                }
                catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show(
                        this,
                        ex.Message,
                        "Could not add file",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }

            files = FilterUnknownFileNodes(files);

            if (files.Count == 0)
            {
                ShowTemporaryStatus("No eligible files added.");
                return;
            }

            AddSelectedFiles(files);
        }

        private void ShowTreeSelectionDialog(FileTreeNode rootNode, IReadOnlyList<string> sourceFolderPaths)
        {
            var nestedLists = BuildNestedLists(rootNode);
            var displayNames = BuildDisplayNames(rootNode);

            var dialog = new DialogTreeSelection(
                "Select Files",
                "Choose the files and folders to add to the bundle.",
                nestedLists,
                displayNames);

            if (dialog.ShowDialog() != true)
            {
                ShowTemporaryStatus("Selection cancelled.");
                return;
            }

            var resolver = new SelectionResolver();
            var selectedFiles = resolver.ResolveSelectedFiles(rootNode, dialog.SelectedIndices);

            if (selectedFiles.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "No files were selected.",
                    "No files selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ShowTemporaryStatus("No files selected.");
                return;
            }

            var filesWithBundlePaths = ApplyMultipleRootPrefixIfNeeded(selectedFiles, sourceFolderPaths);

            AddSelectedFiles(filesWithBundlePaths);
        }

        private void AddSelectedFiles(IEnumerable<FileTreeNode> files)
        {
            var beforeCount = _selectedFiles.Count;
            var filesByFullPath = new Dictionary<string, FileTreeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var existingFile in _selectedFiles)
                filesByFullPath.TryAdd(existingFile.FullPath, existingFile);

            foreach (var file in files)
                filesByFullPath.TryAdd(file.FullPath, file);

            _selectedFiles.Clear();
            _selectedFiles.AddRange(filesByFullPath.Values);

            _selectedFiles.Sort((left, right) =>
            {
                var pathComparison = string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase);

                if (pathComparison != 0)
                    return pathComparison;

                var typeComparison = string.Compare(left.FileType, right.FileType, StringComparison.OrdinalIgnoreCase);

                if (typeComparison != 0)
                    return typeComparison;

                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            var addedCount = _selectedFiles.Count - beforeCount;

            RefreshSelectedFilesList();
            UpdateFileActionButtons();
            UpdateTotalFilesStatus();

            ShowTemporaryStatus(addedCount == 0
                ? "No new files added."
                : $"Added {addedCount} files.");
        }

        private void RefreshSelectedFilesList()
        {
            SelectedFilesListView.ItemsSource = null;
            SelectedFilesListView.ItemsSource = _selectedFiles;
        }

        private void UpdateFileActionButtons()
        {
            CreateBundleButton.IsEnabled = _selectedFiles.Count > 0;
            ClearAllFilesButton.IsEnabled = _selectedFiles.Count > 0;
            RemoveSelectedFilesButton.IsEnabled = SelectedFilesListView.SelectedItems.Count > 0;
        }

        private void UpdateTotalFilesStatus()
        {
            TotalFilesTextBlock.Text = $"Total selected files: {_selectedFiles.Count}";
        }

        private void ShowTemporaryStatus(string message)
        {
            StatusMessageTextBlock.Text = message;
            _statusMessageTimer.Stop();
            _statusMessageTimer.Start();
        }

        private void StatusMessageTimer_Tick(object? sender, EventArgs e)
        {
            _statusMessageTimer.Stop();
            StatusMessageTextBlock.Text = string.Empty;
        }

        private FileTreeNode FilterUnknownFileTypes(FileTreeNode rootNode)
        {
            if (AskAboutUnknownFileTypesCheckBox.IsChecked != true)
                return FilterUnknownFileTypes(rootNode, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var unknownExtensions = GetUnknownExtensions(rootNode);

            if (unknownExtensions.Count == 0)
                return rootNode;

            var dialog = new DialogMultiSelection(
                "Unknown File Types",
                "Select the unknown file extensions you want to include.",
                unknownExtensions);

            var includedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (dialog.ShowDialog() == true)
            {
                foreach (var index in dialog.SelectedIndices)
                    includedExtensions.Add(unknownExtensions[index]);
            }

            return FilterUnknownFileTypes(rootNode, includedExtensions);
        }

        private List<FileTreeNode> FilterUnknownFileNodes(List<FileTreeNode> files)
        {
            var knownFiles = new List<FileTreeNode>();
            var unknownFiles = new List<FileTreeNode>();

            foreach (var file in files)
            {
                if (file.IsKnownFileType)
                {
                    knownFiles.Add(file);
                    continue;
                }

                unknownFiles.Add(file);
            }

            if (unknownFiles.Count == 0)
                return knownFiles;

            if (AskAboutUnknownFileTypesCheckBox.IsChecked != true)
                return knownFiles;

            var unknownExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in unknownFiles)
                unknownExtensions.Add(GetExtensionLabel(file.FullPath));

            var orderedExtensions = new List<string>(unknownExtensions);
            orderedExtensions.Sort(StringComparer.OrdinalIgnoreCase);

            var dialog = new DialogMultiSelection(
                "Unknown File Types",
                "Select the unknown file extensions you want to include.",
                orderedExtensions);

            var includedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (dialog.ShowDialog() == true)
            {
                foreach (var index in dialog.SelectedIndices)
                    includedExtensions.Add(orderedExtensions[index]);
            }

            foreach (var file in unknownFiles)
            {
                if (includedExtensions.Contains(GetExtensionLabel(file.FullPath)))
                    knownFiles.Add(file);
            }

            return knownFiles;
        }

        private static FileTreeNode FilterUnknownFileTypes(FileTreeNode node, HashSet<string> includedExtensions)
        {
            if (node.IsFile)
                return node;

            var children = new List<FileTreeNode>();

            foreach (var child in node.Children)
            {
                if (child.IsFile)
                {
                    if (child.IsKnownFileType || includedExtensions.Contains(GetExtensionLabel(child.FullPath)))
                        children.Add(child);

                    continue;
                }

                var filteredFolder = FilterUnknownFileTypes(child, includedExtensions);

                if (filteredFolder.Children.Count > 0)
                    children.Add(filteredFolder);
            }

            return new FileTreeNode(
                node.DisplayName,
                node.FullPath,
                node.RelativePath,
                false,
                node.FileType,
                node.IsKnownFileType,
                node.DateModified,
                children);
        }

        private static List<FileTreeNode> GetAllFiles(FileTreeNode rootNode)
        {
            var files = new List<FileTreeNode>();

            AddFilesFrom(rootNode);

            return files;

            void AddFilesFrom(FileTreeNode node)
            {
                if (node.IsFile)
                {
                    files.Add(node);
                    return;
                }

                foreach (var child in node.Children)
                    AddFilesFrom(child);
            }
        }

        private static List<FileTreeNode> ApplyMultipleRootPrefixIfNeeded(List<FileTreeNode> files, IReadOnlyList<string> sourceFolderPaths)
        {
            if (sourceFolderPaths.Count <= 1)
                return files;

            var result = new List<FileTreeNode>();

            foreach (var file in files)
            {
                result.Add(new FileTreeNode(
                    file.DisplayName,
                    file.FullPath,
                    $"selected_code/{file.RelativePath}",
                    isFile: true,
                    file.FileType,
                    file.IsKnownFileType,
                    file.DateModified,
                    children: new List<FileTreeNode>()));
            }

            return result;
        }

        private static string? GetCommonRootDirectory(IReadOnlyList<FileTreeNode> files)
        {
            if (files.Count == 0)
                return null;

            var commonPathParts = SplitPath(Path.GetDirectoryName(files[0].FullPath));

            for (var fileIndex = 1; fileIndex < files.Count; fileIndex++)
            {
                var pathParts = SplitPath(Path.GetDirectoryName(files[fileIndex].FullPath));
                var sharedPartCount = 0;
                var maxSharedParts = Math.Min(commonPathParts.Count, pathParts.Count);

                while (sharedPartCount < maxSharedParts &&
                       string.Equals(commonPathParts[sharedPartCount], pathParts[sharedPartCount], StringComparison.OrdinalIgnoreCase))
                {
                    sharedPartCount++;
                }

                commonPathParts = commonPathParts.Take(sharedPartCount).ToList();

                if (commonPathParts.Count == 0)
                    return null;
            }

            var commonRoot = string.Join(Path.DirectorySeparatorChar, commonPathParts);

            if (string.IsNullOrWhiteSpace(commonRoot))
                return null;

            var driveRoot = Path.GetPathRoot(commonRoot)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var trimmedCommonRoot = commonRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(driveRoot, trimmedCommonRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            return commonRoot;
        }

        private static List<string> SplitPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new List<string>();

            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();
        }

        private static string GetBundleNamePrefix(string? commonRootPath)
        {
            if (string.IsNullOrWhiteSpace(commonRootPath))
                return "SelectedCode";

            var folderName = new DirectoryInfo(commonRootPath).Name;

            if (string.IsNullOrWhiteSpace(folderName))
                return "SelectedCode";

            return SanitizeFileName(folderName);
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(fileName.Length);

            foreach (var character in fileName)
            {
                if (invalidCharacters.Contains(character))
                    builder.Append('-');
                else
                    builder.Append(character);
            }

            var sanitizedFileName = builder.ToString().Trim();

            return string.IsNullOrWhiteSpace(sanitizedFileName)
                ? "SelectedCode"
                : sanitizedFileName;
        }

        private static bool HasDroppedFilesOrFolders(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return false;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var path in paths)
            {
                if (Directory.Exists(path) || File.Exists(path))
                    return true;
            }

            return false;
        }

        private static List<string> GetUnknownExtensions(FileTreeNode rootNode)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CollectUnknownExtensions(rootNode, extensions);

            var orderedExtensions = new List<string>(extensions);
            orderedExtensions.Sort(StringComparer.OrdinalIgnoreCase);

            return orderedExtensions;
        }

        private static void CollectUnknownExtensions(FileTreeNode node, HashSet<string> extensions)
        {
            if (node.IsFile)
            {
                if (!node.IsKnownFileType)
                    extensions.Add(GetExtensionLabel(node.FullPath));

                return;
            }

            foreach (var child in node.Children)
                CollectUnknownExtensions(child, extensions);
        }

        private static List<object> BuildNestedLists(FileTreeNode rootNode)
        {
            var items = new List<object>();

            foreach (var child in rootNode.Children)
                items.Add(BuildNestedItem(child));

            return items;
        }

        private static object BuildNestedItem(FileTreeNode node)
        {
            if (node.IsFile)
                return node.FullPath;

            var items = new List<object>();

            foreach (var child in node.Children)
                items.Add(BuildNestedItem(child));

            return items;
        }

        private static List<object> BuildDisplayNames(FileTreeNode rootNode)
        {
            var names = new List<object>();

            foreach (var child in rootNode.Children)
                names.Add(BuildDisplayName(child));

            return names;
        }

        private static object BuildDisplayName(FileTreeNode node)
        {
            if (node.IsFile)
                return node.DisplayName;

            var names = new List<object>
            {
                $"{node.DisplayName} <Folder>"
            };

            foreach (var child in node.Children)
                names.Add(BuildDisplayName(child));

            return names;
        }

        private static string GetExtensionLabel(string fullPath)
        {
            var extension = Path.GetExtension(fullPath);

            return string.IsNullOrWhiteSpace(extension) ? "(no extension)" : extension.ToLowerInvariant();
        }

        private static int CountFiles(FileTreeNode node)
        {
            if (node.IsFile)
                return 1;

            var count = 0;

            foreach (var child in node.Children)
                count += CountFiles(child);

            return count;
        }
    }
}