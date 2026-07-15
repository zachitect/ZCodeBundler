using Microsoft.Win32;
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ZCodeBundler.Bundling;
using ZCodeBundler.Decoding;
using ZCodeBundler.Dialogs;
using ZCodeBundler.Dialogs.TreeSelection;

namespace ZCodeBundler
{
    public partial class MainWindow : Window
    {
        private readonly List<FileTreeNode> _selectedFiles = new();
        private readonly List<DecodedBundleListItem> _decodedFiles = new();
        private readonly Dictionary<string, DecodedBundleViewerWindow> _openDecodedViewers = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _statusMessageTimer;
        private readonly ZCodeBundleReader _bundleReader = new();
        private bool _isDecodedPanelExpanded;

        private const string EmbeddedPrompt = """
FINAL OUTPUT CONTRACT

This contract applies only when the user provides an input .zcb.txt bundle.

If no input .zcb.txt bundle was provided, do not create a .zcb.txt output file.

The input .zcb.txt bundle contains file blocks. For each file you need to output, use only:
- PATH from the matching input file block
- the original source content from the matching input file block

Do not treat bundle headers, metadata lines, or delimiter lines as source code.

If no files need to be output, create no output file.

If one or more files need to be output, create exactly one plain-text output file with extension:
.zcb.txt

The output .zcb.txt file must contain only files that need to be output.
Do not include unchanged files.
Do not include the full input bundle.

Each output file must use this exact block format:
--- ZCODEBUNDLE_FILE_START ---
PATH: 
--- ZCODEBUNDLE_CONTENT_START ---
--- ZCODEBUNDLE_FILE_END ---

Rules:
- Use one block per output file.
- PATH must be copied exactly from the matching input file block.
- Do not invent, guess, shorten, normalize, relativize, rename, or alter PATH.
- The content section must contain the complete final file content for that PATH.
- Do not output snippets, partial files, diffs, patches, ellipses, or “unchanged” placeholders.
- Do not include CONTENT_LENGTH.
- Do not include FILE_NAME, TYPE, DATE_MODIFIED, FILE_COUNT, BUNDLE_TITLE, ZCODEBUNDLE_VERSION, ZCODEBUNDLE_OUTPUT_VERSION, ROOT_PATH, GENERATED_AT, or any header.
- Do not wrap the output in Markdown or code fences.
- Do not add explanations, summaries, comments, or text before, between, or after file blocks.
- Preserve the final file content exactly as source text. Do not let the tool, script, template engine, serializer, or host language reinterpret escape sequences, string/template literals, regexes, quotes, braces, backslashes, tabs, newlines, or other language-specific syntax. For example, if the intended file content contains the two characters backslash+n, output backslash+n, not an actual newline.
- Before delivering the output file, inspect the generated .zcb.txt itself, not only the generation script or plan. Verify that each changed file block contains the intended final source text and that no string/template literal, escape sequence, delimiter, indentation, or line break was accidentally transformed by output generation.
- If the complete final file content would contain a standalone line exactly equal to --- ZCODEBUNDLE_FILE_END ---, do not create a .zcb.txt output file.
""";

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
            ShowDecodedBundleFiles(new List<DecodedBundleListItem>());
            SetDecodedPanelExpanded(false);
            UpdateAutoFillListColumns();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_openDecodedViewers.Count > 0)
            {
                var confirmed = new MessageDialog(
                    "Close ZCodeBundler",
                    "Closing ZCodeBundler will close all decoded viewer windows. Continue?",
                    "Yes",
                    MessageBoxImage.Warning)
                { Owner = this }.ShowDialog();

                if (confirmed != true)
                {
                    e.Cancel = true;
                    return;
                }

                CloseAllDecodedViewers();
            }

            base.OnClosing(e);
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

            var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var commonRootPath = GetCommonRootDirectory(_selectedFiles);
            var bundleNamePrefix = GetBundleNamePrefix(commonRootPath);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var defaultFileName = $"{bundleNamePrefix}-{timestamp}.zcb.txt";

            var dialog = new SaveFileDialog
            {
                Title = "Save ZCodeBundle",
                FileName = defaultFileName,
                InitialDirectory = Directory.Exists(downloadsFolder) ? downloadsFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
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

                new MessageDialog(
                    $"Bundle created",
                    message,
                    "OK",
                    result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning)
                { Owner = this }.ShowDialog();

                ShowTemporaryStatus($"Bundle created. Files written: {result.WrittenFileCount}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                new MessageDialog(
                    $"Could not create bundle",
                    ex.Message,
                    "OK",
                    MessageBoxImage.Error)
                { Owner = this }.ShowDialog();

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

        private void ToggleDecodedPanelButton_Click(object sender, RoutedEventArgs e)
        {
            SetDecodedPanelExpanded(!_isDecodedPanelExpanded);
        }

        private void ViewPromptButton_Click(object sender, RoutedEventArgs e)
        {
            var promptTextBox = new System.Windows.Controls.TextBox
            {
                Text = EmbeddedPrompt,
                IsReadOnly = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(10)
            };

            var copyButton = new System.Windows.Controls.Button
            {
                Content = "Copy All",
                MinWidth = 90,
                Margin = new Thickness(0, 0, 8, 0)
            };

            copyButton.Click += (_, _) => Clipboard.SetText(EmbeddedPrompt);

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "Close",
                MinWidth = 90,
                IsCancel = true
            };

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(closeButton);

            var layout = new System.Windows.Controls.DockPanel { Margin = new Thickness(12) };
            System.Windows.Controls.DockPanel.SetDock(buttonPanel, System.Windows.Controls.Dock.Bottom);
            layout.Children.Add(buttonPanel);
            layout.Children.Add(promptTextBox);

            var window = new Window
            {
                Title = "Embedded Prompt",
                Owner = this,
                Width = 800,
                Height = 600,
                MinWidth = 500,
                MinHeight = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = layout
            };

            closeButton.Click += (_, _) => window.Close();
            window.ShowDialog();
        }

        private void AddZcbTxtButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Add ZCodeBundle file",
                Multiselect = true,
                DefaultExt = ".txt",
                Filter = "ZCodeBundle files (*.zcb.txt)|*.zcb.txt|Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            TryDecodeDroppedBundleFiles(dialog.FileNames);
        }

        private void ClearDecodedBundleButton_Click(object sender, RoutedEventArgs e)
        {
            CloseAllDecodedViewers();
            ShowDecodedBundleFiles(new List<DecodedBundleListItem>());
            ShowTemporaryStatus("Cleared decoded files.");
        }

        private void ApplyAllDecodedChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_decodedFiles.Count == 0)
                return;

            RefreshDecodedFileStatuses(_decodedFiles);

            if (_decodedFiles.Any(file => file.Status == DecodedSourceStatus.DuplicateTarget))
            {
                RefreshDecodedBundleFilesList();
                UpdateDecodedPanelButtons();
                RefreshAllOpenDecodedViewers();
                ShowDuplicateTargetWarning();
                return;
            }

            ApplyDecodedFiles(_decodedFiles, "There are no decoded files with changes to apply.", "Apply decoded changes to all files?");
        }

        private void ApplySelectedDecodedChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (DecodedBundleFilesListView.SelectedItems.Count == 0)
                return;

            var selectedItems = GetSelectedDecodedFiles();

            if (selectedItems.Count == 0)
                return;

            RefreshDecodedFileStatuses(_decodedFiles);

            if (selectedItems.Any(file => file.Status == DecodedSourceStatus.DuplicateTarget))
            {
                RefreshDecodedBundleFilesList();
                UpdateDecodedPanelButtons();
                RefreshAllOpenDecodedViewers();
                ShowDuplicateTargetWarning();
                return;
            }

            ApplyDecodedFiles(selectedItems, "There are no selected decoded files with changes to apply.", "Apply decoded changes to selected files?");
        }

        private void RemoveSelectedDecodedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (DecodedBundleFilesListView.SelectedItems.Count == 0)
                return;

            var selectedItems = new List<DecodedBundleListItem>();

            foreach (var item in DecodedBundleFilesListView.SelectedItems)
            {
                if (item is DecodedBundleListItem decodedFile)
                    selectedItems.Add(decodedFile);
            }

            if (selectedItems.Count == 0)
                return;

            CloseDecodedViewersFor(selectedItems);

            foreach (var decodedFile in selectedItems)
                _decodedFiles.Remove(decodedFile);

            RefreshDecodedFileStatuses(_decodedFiles);
            RefreshDecodedBundleFilesList();
            UpdateDecodedPanelButtons();
            RefreshAllOpenDecodedViewers();

            ShowTemporaryStatus($"Removed {selectedItems.Count} decoded file(s).");
        }

        private void CloseAllDecodedViewersButton_Click(object sender, RoutedEventArgs e)
        {
            CloseAllDecodedViewers();
        }

        private void DecodedBundleFilesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateDecodedPanelButtons();
        }

        private List<DecodedBundleListItem> GetSelectedDecodedFiles()
        {
            var selectedItems = new List<DecodedBundleListItem>();

            foreach (var item in DecodedBundleFilesListView.SelectedItems)
            {
                if (item is DecodedBundleListItem decodedFile)
                    selectedItems.Add(decodedFile);
            }

            return selectedItems;
        }

        private void ApplyDecodedFiles(List<DecodedBundleListItem> decodedFiles, string noApplicableMessage, string confirmationTitle)
        {
            var differentFiles = decodedFiles.Where(file => file.Status == DecodedSourceStatus.Different).ToList();
            var missingFiles = decodedFiles.Where(file => file.Status == DecodedSourceStatus.Missing).ToList();

            if (differentFiles.Count == 0 && missingFiles.Count == 0)
            {
                RefreshDecodedBundleFilesList();
                UpdateDecodedPanelButtons();
                RefreshAllOpenDecodedViewers();
                ShowInformationMessage("Apply changes", noApplicableMessage);
                return;
            }

            var filesToApply = differentFiles.Concat(missingFiles).ToList();
            var sourceSnapshots = new Dictionary<DecodedBundleListItem, SourceSnapshot>();

            foreach (var decodedFile in filesToApply)
            {
                var sourceContent = GetSourceContentForViewer(decodedFile, out var sourceState);
                sourceSnapshots[decodedFile] = new SourceSnapshot(sourceState, sourceContent);
            }

            var confirmationMessage = BuildApplyConfirmationMessage(decodedFiles, differentFiles, missingFiles);
            var confirmed = new MessageDialog(
                confirmationTitle,
                confirmationMessage,
                "Yes",
                MessageBoxImage.Warning)
            { Owner = this }.ShowDialog();

            if (confirmed != true)
            {
                RefreshDecodedBundleFilesList();
                UpdateDecodedPanelButtons();
                RefreshAllOpenDecodedViewers();
                return;
            }

            var appliedCount = 0;

            foreach (var decodedFile in filesToApply)
            {
                if (!TryWriteDecodedContent(decodedFile, sourceSnapshots[decodedFile], out var errorMessage))
                {
                    RefreshDecodedFileStatuses(_decodedFiles);
                    RefreshDecodedBundleFilesList();
                    UpdateDecodedPanelButtons();
                    RefreshAllOpenDecodedViewers();

                    var failureMessage = appliedCount == 0
                        ? errorMessage
                        : $"Applied {appliedCount} file(s), then stopped at the first write failure. {errorMessage}";

                    ShowErrorMessage("Could not apply decoded file", failureMessage);
                    return;
                }

                appliedCount++;
            }

            RefreshDecodedFileStatuses(_decodedFiles);
            RefreshDecodedBundleFilesList();
            UpdateDecodedPanelButtons();
            RefreshAllOpenDecodedViewers();
            ShowTemporaryStatus($"Applied {appliedCount} decoded file(s).");
        }

        private static string BuildApplyConfirmationMessage(
            List<DecodedBundleListItem> decodedFiles,
            List<DecodedBundleListItem> differentFiles,
            List<DecodedBundleListItem> missingFiles)
        {
            var unchangedCount = decodedFiles.Count(file => file.Status == DecodedSourceStatus.Same);
            var invalidPathCount = decodedFiles.Count(file => file.Status == DecodedSourceStatus.InvalidPath);
            var sourceReadErrorCount = decodedFiles.Count(file => file.Status == DecodedSourceStatus.SourceReadError);
            var builder = new StringBuilder();

            builder.AppendLine($"Existing files to replace: {differentFiles.Count}");
            builder.AppendLine($"Missing files to create: {missingFiles.Count}");
            builder.AppendLine($"Unchanged files skipped: {unchangedCount}");
            builder.AppendLine($"Invalid paths skipped: {invalidPathCount}");
            builder.AppendLine($"Source read errors skipped: {sourceReadErrorCount}");
            builder.AppendLine();

            AppendPathPreview(builder, "Replace paths", differentFiles);
            AppendPathPreview(builder, "Create paths", missingFiles);

            builder.AppendLine("Continue?");
            return builder.ToString();
        }

        private static void AppendPathPreview(StringBuilder builder, string title, List<DecodedBundleListItem> files)
        {
            if (files.Count == 0)
                return;

            builder.AppendLine(title + ":");

            foreach (var file in files.Take(10))
                builder.AppendLine(file.Path);

            if (files.Count > 10)
                builder.AppendLine($"...and {files.Count - 10} more files.");

            builder.AppendLine();
        }

        private static bool TryWriteDecodedContent(DecodedBundleListItem decodedFile, SourceSnapshot expectedSource, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!TryGetNormalizedTargetPath(decodedFile.Path, out var normalizedPath))
            {
                errorMessage = $"Invalid source path:\n{decodedFile.Path}";
                return false;
            }

            try
            {
                var parentFolderPath = Path.GetDirectoryName(normalizedPath);

                if (expectedSource.State == DecodedBundleViewerWindow.SourceSnapshotState.Missing)
                {
                    if (!string.IsNullOrWhiteSpace(parentFolderPath))
                        Directory.CreateDirectory(parentFolderPath);

                    using var newStream = new FileStream(normalizedPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    using var newWriter = new StreamWriter(newStream, new UTF8Encoding(false));
                    newWriter.Write(decodedFile.Content);
                    return true;
                }

                if (expectedSource.State != DecodedBundleViewerWindow.SourceSnapshotState.Exists)
                {
                    errorMessage = $"The source file is not in a writable state:\n{normalizedPath}";
                    return false;
                }

                using var stream = new FileStream(normalizedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                var hasUtf8Bom = HasUtf8Bom(stream);
                var currentContent = ReadUtf8Text(stream);

                if (!string.Equals(currentContent, expectedSource.Content, StringComparison.Ordinal))
                {
                    errorMessage = $"The source file changed after confirmation and was not overwritten:\n{normalizedPath}";
                    return false;
                }

                stream.Position = 0;
                stream.SetLength(0);

                using var writer = new StreamWriter(stream, new UTF8Encoding(hasUtf8Bom), leaveOpen: true);
                writer.Write(decodedFile.Content);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or DirectoryNotFoundException or PathTooLongException or NotSupportedException or DecoderFallbackException)
            {
                errorMessage = $"Could not write decoded content to:\n{normalizedPath}\n\n{ex.Message}";
                return false;
            }
        }

        private static string ReadUtf8Text(Stream stream)
        {
            stream.Position = 0;
            var strictUtf8 = new UTF8Encoding(false, true);

            using var reader = new StreamReader(stream, strictUtf8, false, leaveOpen: true);
            var content = reader.ReadToEnd();

            if (content.Contains('\0'))
                throw new DecoderFallbackException("The source file is not UTF-8 text.");

            return content.StartsWith('\uFEFF') ? content[1..] : content;
        }

        private static bool HasUtf8Bom(Stream stream)
        {
            if (stream.Length < 3)
                return false;

            stream.Position = 0;
            var hasBom = stream.ReadByte() == 0xEF
                && stream.ReadByte() == 0xBB
                && stream.ReadByte() == 0xBF;
            stream.Position = 0;
            return hasBom;
        }

        private void ShowDuplicateTargetWarning()
        {
            ShowInformationMessage(
                "Duplicate target paths",
                "Multiple decoded files target the same source path. Remove the unwanted duplicates before applying changes.");
        }

        private void ShowInformationMessage(string title, string message)
        {
            new MessageDialog(
                title,
                message,
                "OK",
                MessageBoxImage.Information)
            { Owner = this }.ShowDialog();
        }

        private void ShowErrorMessage(string title, string message)
        {
            new MessageDialog(
                title,
                message,
                "OK",
                MessageBoxImage.Error)
            { Owner = this }.ShowDialog();
        }

        private void SetDecodedPanelExpanded(bool isExpanded)
        {
            if (!isExpanded)
            {
                HideDecodedPanel();
                return;
            }

            if (_isDecodedPanelExpanded)
            {
                ShowDecodedPanelPreservingWidth();
                return;
            }

            ResetDecodedPanelWidth();
        }

        private void ResetDecodedPanelWidth()
        {
            _isDecodedPanelExpanded = true;
            BundlerPanelColumn.Width = new GridLength(1, GridUnitType.Star);
            DecodedPanelColumn.Width = new GridLength(1, GridUnitType.Star);
            DecodedPanelSplitterColumn.Width = new GridLength(8);
            DecodedPanelBorder.Visibility = Visibility.Visible;
            DecodedPanelGridSplitter.Visibility = Visibility.Visible;
            ToggleDecodedPanelButton.Content = "◀";
            UpdateAutoFillListColumns();
        }

        private void ShowDecodedPanelPreservingWidth()
        {
            _isDecodedPanelExpanded = true;
            DecodedPanelSplitterColumn.Width = new GridLength(8);
            DecodedPanelBorder.Visibility = Visibility.Visible;
            DecodedPanelGridSplitter.Visibility = Visibility.Visible;
            ToggleDecodedPanelButton.Content = "◀";
            UpdateAutoFillListColumns();
        }

        private void HideDecodedPanel()
        {
            _isDecodedPanelExpanded = false;
            BundlerPanelColumn.Width = new GridLength(1, GridUnitType.Star);
            DecodedPanelColumn.Width = new GridLength(0);
            DecodedPanelSplitterColumn.Width = new GridLength(0);
            DecodedPanelBorder.Visibility = Visibility.Collapsed;
            DecodedPanelGridSplitter.Visibility = Visibility.Collapsed;
            ToggleDecodedPanelButton.Content = "▶";
            UpdateAutoFillListColumns();
        }

        private void DecodedPanelGridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ResetDecodedPanelWidth();
        }

        private void ListDropGridSplitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectedFilesAreaRow.Height = new GridLength(1, GridUnitType.Star);
            DropAreaRow.Height = new GridLength(1, GridUnitType.Star);
        }

        private void MainListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAutoFillListColumns();
        }

        private void UpdateAutoFillListColumns()
        {
            AutoFillLastColumn(SelectedFilesListView, SelectedFilesFileColumn, 220, SelectedFilesCodeTypeColumn);
            AutoFillLastColumn(
                DecodedBundleFilesListView,
                DecodedBundleColumn,
                180,
                DecodedStatusColumn,
                DecodedCodeTypeColumn,
                DecodedFileColumn,
                DecodedModifiedColumn);
        }

        private static void AutoFillLastColumn(
            System.Windows.Controls.ListView listView,
            System.Windows.Controls.GridViewColumn lastColumn,
            double minimumWidth,
            params System.Windows.Controls.GridViewColumn[] precedingColumns)
        {
            const double widthReserve = 32;
            if (listView.ActualWidth <= 0)
                return;

            var usedWidth = widthReserve;
            foreach (var column in precedingColumns)
                usedWidth += column.Width;

            var remainingWidth = listView.ActualWidth - usedWidth;
            lastColumn.Width = Math.Max(minimumWidth, remainingWidth);
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

            var bundlePaths = paths.Where(IsZCodeBundleFile).ToArray();
            var normalPaths = paths.Where(path => !IsZCodeBundleFile(path)).ToArray();

            if (bundlePaths.Length > 0)
                TryDecodeDroppedBundleFiles(bundlePaths);

            AddDroppedFilesAndFolders(normalPaths);
        }

        private void AddDroppedFilesAndFolders(string[] paths)
        {
            if (paths.Length == 0)
                return;

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
                new MessageDialog(
                    "Invalid drop",
                    "Drop folders or files.",
                    "OK",
                    MessageBoxImage.Information)
                { Owner = this }.ShowDialog();
                ShowTemporaryStatus("Invalid drop.");
            }
        }

        private bool TryDecodeDroppedBundleFiles(string[] paths)
        {
            var bundlePaths = paths.Where(IsZCodeBundleFile).ToList();
            if (bundlePaths.Count == 0)
                return false;

            CloseAllDecodedViewers();
            ShowDecodedBundleFiles(new List<DecodedBundleListItem>());

            var decodedItems = new List<DecodedBundleListItem>();
            var decodedItemIndex = 0;

            try
            {
                foreach (var bundlePath in bundlePaths)
                {
                    var decodedFiles = _bundleReader.Read(bundlePath);

                    foreach (var decodedFile in decodedFiles)
                    {
                        decodedItems.Add(new DecodedBundleListItem(bundlePath, decodedItemIndex, decodedFile));
                        decodedItemIndex++;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or SecurityException
                or PathTooLongException
                or NotSupportedException)
            {
                new MessageDialog(
                    "Could not decode bundle",
                    ex.Message,
                    "OK",
                    MessageBoxImage.Error)
                { Owner = this }.ShowDialog();

                ShowTemporaryStatus("Could not decode bundle.");
                return true;
            }

            ShowDecodedBundleFiles(decodedItems);
            SetDecodedPanelExpanded(true);
            ShowTemporaryStatus($"Decoded {decodedItems.Count} files from {bundlePaths.Count} bundle file(s).");
            return true;
        }

        private void ShowDecodedBundleFiles(List<DecodedBundleListItem> decodedFiles)
        {
            _decodedFiles.Clear();
            _decodedFiles.AddRange(decodedFiles);
            RefreshDecodedFileStatuses(_decodedFiles);
            RefreshDecodedBundleFilesList();
            UpdateDecodedPanelButtons();
        }

        private void RefreshDecodedBundleFilesList()
        {
            DecodedBundleFilesListView.ItemsSource = null;
            DecodedBundleFilesListView.ItemsSource = _decodedFiles;
            DecodedBundleFilesCountTextBlock.Text = $"{_decodedFiles.Count} files";
            UpdateAutoFillListColumns();
        }

        private void UpdateDecodedPanelButtons()
        {
            var hasDecodedFiles = _decodedFiles.Count > 0;
            var hasSelectedDecodedFiles = DecodedBundleFilesListView.SelectedItems.Count > 0;

            ClearDecodedBundleButton.IsEnabled = hasDecodedFiles;
            ApplyAllDecodedChangesButton.IsEnabled = hasDecodedFiles;
            ApplySelectedDecodedChangesButton.IsEnabled = hasSelectedDecodedFiles;
            RemoveSelectedDecodedFilesButton.IsEnabled = hasDecodedFiles && hasSelectedDecodedFiles;
            CloseAllDecodedViewersButton.IsEnabled = _openDecodedViewers.Count > 0;
        }

        private static void RefreshDecodedFileStatuses(List<DecodedBundleListItem> decodedFiles)
        {
            var normalizedPathsByItem = new Dictionary<DecodedBundleListItem, string>();

            foreach (var decodedFile in decodedFiles)
            {
                decodedFile.Status = GetSourceStatus(decodedFile, out var normalizedPath, out var statusDetail);
                decodedFile.StatusDetail = statusDetail;

                if (!string.IsNullOrWhiteSpace(normalizedPath))
                    normalizedPathsByItem[decodedFile] = normalizedPath;
            }

            var targetCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var normalizedPath in normalizedPathsByItem.Values)
            {
                targetCounts.TryGetValue(normalizedPath, out var count);
                targetCounts[normalizedPath] = count + 1;
            }

            foreach (var decodedFile in decodedFiles)
            {
                if (!normalizedPathsByItem.TryGetValue(decodedFile, out var normalizedPath))
                    continue;

                if (targetCounts[normalizedPath] <= 1)
                    continue;

                decodedFile.Status = DecodedSourceStatus.DuplicateTarget;
                decodedFile.StatusDetail = "Multiple decoded files target the same source path.";
            }
        }

        private static DecodedSourceStatus GetSourceStatus(DecodedBundleListItem decodedFile, out string? normalizedPath, out string statusDetail)
        {
            if (!TryGetNormalizedTargetPath(decodedFile.Path, out normalizedPath))
            {
                statusDetail = "PATH is not a valid absolute source file path.";
                return DecodedSourceStatus.InvalidPath;
            }

            try
            {
                using var stream = File.OpenRead(normalizedPath);
                var sourceContent = ReadUtf8Text(stream);

                statusDetail = string.Empty;
                return string.Equals(sourceContent, decodedFile.Content, StringComparison.Ordinal)
                    ? DecodedSourceStatus.Same
                    : DecodedSourceStatus.Different;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                statusDetail = string.Empty;
                return DecodedSourceStatus.Missing;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException or NotSupportedException or DecoderFallbackException)
            {
                statusDetail = ex.Message;
                return DecodedSourceStatus.SourceReadError;
            }
        }

        private static bool TryGetNormalizedTargetPath(string targetPath, out string? normalizedPath)
        {
            normalizedPath = null;

            if (string.IsNullOrWhiteSpace(targetPath))
                return false;

            try
            {
                if (!Path.IsPathFullyQualified(targetPath))
                    return false;

                normalizedPath = Path.GetFullPath(targetPath);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
            {
                return false;
            }
        }

        private void DecodedBundleFilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DecodedBundleFilesListView.SelectedItem is not DecodedBundleListItem decodedFile)
                return;

            RefreshDecodedFileStatuses(_decodedFiles);
            RefreshDecodedBundleFilesList();
            UpdateDecodedPanelButtons();
            OpenDecodedViewer(decodedFile);
        }

        private void OpenDecodedViewer(DecodedBundleListItem decodedFile)
        {
            var viewerKey = GetDecodedViewerKey(decodedFile);

            if (_openDecodedViewers.TryGetValue(viewerKey, out var existingViewer))
            {
                if (existingViewer.WindowState == WindowState.Minimized)
                    existingViewer.WindowState = WindowState.Normal;

                existingViewer.Activate();
                return;
            }

            var sourceContent = GetSourceContentForViewer(decodedFile, out var sourceState);
            var viewer = new DecodedBundleViewerWindow(
                decodedFile,
                sourceState,
                sourceContent,
                GetViewerStatusText(decodedFile),
                ApplyDecodedViewerChanges)
            {
                Owner = this
            };

            viewer.Closed += (_, _) =>
            {
                _openDecodedViewers.Remove(viewerKey);
                UpdateDecodedPanelButtons();
            };

            _openDecodedViewers[viewerKey] = viewer;
            viewer.Show();
            UpdateDecodedPanelButtons();
        }

        private void ApplyDecodedViewerChanges(DecodedBundleViewerWindow viewer)
        {
            var decodedFile = viewer.DecodedFile;
            RefreshDecodedFileStatuses(_decodedFiles);
            RefreshDecodedBundleFilesList();
            UpdateDecodedPanelButtons();
            RefreshDecodedViewer(viewer);

            var currentSourceContent = GetSourceContentForViewer(decodedFile, out var currentSourceState);

            if (decodedFile.Status == DecodedSourceStatus.DuplicateTarget)
            {
                ShowDuplicateTargetWarning();
                return;
            }

            if (decodedFile.Status == DecodedSourceStatus.Same)
            {
                ShowInformationMessage("Apply decoded changes", "The source file already matches the decoded bundle content. No changes were written.");
                return;
            }

            if (decodedFile.Status == DecodedSourceStatus.InvalidPath)
            {
                ShowErrorMessage("Cannot apply decoded file", GetViewerStatusText(decodedFile));
                return;
            }

            if (decodedFile.Status == DecodedSourceStatus.SourceReadError)
            {
                ShowErrorMessage("Cannot apply decoded file", GetViewerStatusText(decodedFile));
                return;
            }

            if (decodedFile.Status is not (DecodedSourceStatus.Different or DecodedSourceStatus.Missing))
                return;

            var confirmationMessage = GetViewerApplyConfirmationMessage(viewer, currentSourceState, currentSourceContent);
            var confirmed = new MessageDialog(
                "Apply decoded changes",
                confirmationMessage,
                "Yes",
                MessageBoxImage.Warning)
            { Owner = this }.ShowDialog();

            if (confirmed != true)
            {
                RefreshDecodedFileStatuses(_decodedFiles);
                RefreshDecodedBundleFilesList();
                UpdateDecodedPanelButtons();
                RefreshAllOpenDecodedViewers();
                return;
            }

            if (!TryWriteDecodedContent(decodedFile, new SourceSnapshot(currentSourceState, currentSourceContent), out var errorMessage))
            {
                RefreshDecodedFileStatuses(_decodedFiles);
                RefreshDecodedBundleFilesList();
                UpdateDecodedPanelButtons();
                RefreshAllOpenDecodedViewers();
                ShowErrorMessage("Could not apply decoded file", errorMessage);
                return;
            }

            RefreshDecodedFileStatuses(_decodedFiles);
            RefreshDecodedBundleFilesList();
            UpdateDecodedPanelButtons();
            RefreshAllOpenDecodedViewers();
            ShowTemporaryStatus($"Applied decoded file: {decodedFile.DisplayName}");
        }

        private static string GetViewerApplyConfirmationMessage(
            DecodedBundleViewerWindow viewer,
            DecodedBundleViewerWindow.SourceSnapshotState currentSourceState,
            string currentSourceContent)
        {
            var targetPath = viewer.DecodedFile.Path;

            if (viewer.CurrentStatus == DecodedSourceStatus.Different)
            {
                if (viewer.OpenedSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Exists
                    && currentSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Exists
                    && !string.Equals(currentSourceContent, viewer.OpenedSourceContent, StringComparison.Ordinal))
                {
                    return $"The source file changed since this viewer opened.\nTarget path:\n{targetPath}\n\nApplying will replace the current file content with decoded bundle content.\nContinue?";
                }

                if (viewer.OpenedSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Missing
                    && currentSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Exists)
                {
                    return $"The source file was missing when this viewer opened, but it now exists.\nTarget path:\n{targetPath}\n\nApplying will replace it with decoded bundle content.\nContinue?";
                }

                return $"Apply decoded content to this file?\nTarget path:\n{targetPath}\n\nThe existing file content will be replaced.\nContinue?";
            }

            if (viewer.OpenedSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Exists
                && currentSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Missing)
            {
                return $"The source file existed when this viewer opened, but it is now missing.\nTarget path:\n{targetPath}\n\nApplying will recreate it from decoded bundle content.\nContinue?";
            }

            if (viewer.OpenedSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Missing
                && currentSourceState == DecodedBundleViewerWindow.SourceSnapshotState.Exists)
            {
                return $"The source file was missing when this viewer opened, but it now exists.\nTarget path:\n{targetPath}\n\nApplying will replace it with decoded bundle content.\nContinue?";
            }

            return $"Create this source file from decoded content?\nTarget path:\n{targetPath}\n\nMissing parent folders will be created if needed.\nContinue?";
        }

        private void RefreshAllOpenDecodedViewers()
        {
            foreach (var viewer in _openDecodedViewers.Values.ToList())
                RefreshDecodedViewer(viewer);
        }

        private void RefreshDecodedViewer(DecodedBundleViewerWindow viewer)
        {
            var sourceContent = GetSourceContentForViewer(viewer.DecodedFile, out _);
            viewer.Refresh(sourceContent, GetViewerStatusText(viewer.DecodedFile), viewer.DecodedFile.Status);
        }

        private void CloseAllDecodedViewers()
        {
            foreach (var viewer in _openDecodedViewers.Values.ToList())
                viewer.Close();

            _openDecodedViewers.Clear();
            UpdateDecodedPanelButtons();
        }

        private void CloseDecodedViewersFor(List<DecodedBundleListItem> decodedFiles)
        {
            foreach (var decodedFile in decodedFiles)
            {
                var viewerKey = GetDecodedViewerKey(decodedFile);

                if (_openDecodedViewers.TryGetValue(viewerKey, out var viewer))
                    viewer.Close();
            }

            UpdateDecodedPanelButtons();
        }

        private static string GetDecodedViewerKey(DecodedBundleListItem decodedFile)
        {
            return decodedFile.BundlePath + "\u001F" + decodedFile.Path;
        }

        private static string GetSourceContentForViewer(
            DecodedBundleListItem decodedFile,
            out DecodedBundleViewerWindow.SourceSnapshotState sourceState)
        {
            sourceState = DecodedBundleViewerWindow.SourceSnapshotState.Exists;

            if (!TryGetNormalizedTargetPath(decodedFile.Path, out var normalizedPath))
            {
                sourceState = DecodedBundleViewerWindow.SourceSnapshotState.InvalidPath;
                return "PATH is not a valid absolute source path.";
            }

            try
            {
                using var stream = File.OpenRead(normalizedPath);
                return ReadUtf8Text(stream);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                sourceState = DecodedBundleViewerWindow.SourceSnapshotState.Missing;
                return string.Empty;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or PathTooLongException or NotSupportedException or DecoderFallbackException)
            {
                sourceState = DecodedBundleViewerWindow.SourceSnapshotState.SourceReadError;
                return $"Source file could not be read.\n{ex.Message}";
            }
        }

        private static string GetViewerStatusText(DecodedBundleListItem decodedFile)
        {
            return decodedFile.Status switch
            {
                DecodedSourceStatus.Same => "Same: source content exactly matches decoded content.",
                DecodedSourceStatus.Different => "Different: source content differs from decoded content.",
                DecodedSourceStatus.Missing => "Missing: source file does not exist. Decoded content is shown as added content.",
                DecodedSourceStatus.InvalidPath => "InvalidPath: PATH is not a valid absolute source path.",
                DecodedSourceStatus.DuplicateTarget => "DuplicateTarget: multiple decoded files target this source path, so apply is blocked.",
                DecodedSourceStatus.SourceReadError => string.IsNullOrWhiteSpace(decodedFile.StatusDetail)
                    ? "SourceReadError: source file could not be read."
                    : $"SourceReadError: source file could not be read. {decodedFile.StatusDetail}",
                _ => decodedFile.Status.ToString()
            };
        }

        private static bool IsZCodeBundleFile(string path)
        {
            return File.Exists(path) && path.EndsWith(".zcb.txt", StringComparison.OrdinalIgnoreCase);
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
                    new MessageDialog(
                        "No files found",
                        "No eligible files were found in the selected folder.",
                        "OK",
                        MessageBoxImage.Information)
                    { Owner = this }.ShowDialog();
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
                new MessageDialog(
                    "Could not scan folder",
                    ex.Message,
                    "OK",
                    MessageBoxImage.Error)
                { Owner = this }.ShowDialog();
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
                    new MessageDialog(
                        "Could not add file",
                        ex.Message,
                        "OK",
                        MessageBoxImage.Error)
                    { Owner = this }.ShowDialog();
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

            var dialog = new TreeSelectionWindow("Select Files", "Choose the files and folders to add to the bundle.", nestedLists, displayNames) { Owner = this };

            if (dialog.ShowDialog() != true)
            {
                ShowTemporaryStatus("Selection cancelled.");
                return;
            }

            var resolver = new SelectionResolver();
            var selectedFiles = resolver.ResolveSelectedFiles(rootNode, dialog.SelectedIndices);

            if (selectedFiles.Count == 0)
            {
                new MessageDialog(
                    "No files selected",
                    "No files were selected.",
                    "OK",
                    MessageBoxImage.Information)
                { Owner = this }.ShowDialog();
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
            ShowTemporaryStatus(addedCount == 0 ? "No new files added." : $"Added {addedCount} files.");
        }

        private void RefreshSelectedFilesList()
        {
            SelectedFilesListView.ItemsSource = null;
            SelectedFilesListView.ItemsSource = _selectedFiles;
            UpdateAutoFillListColumns();
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

            var dialog = new MultiSelectionWindow("Unknown File Types", "Select the unknown file extensions you want to include.", unknownExtensions) { Owner = this };
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

            var dialog = new MultiSelectionWindow("Unknown File Types", "Select the unknown file extensions you want to include.", orderedExtensions) { Owner = this };
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

            return new FileTreeNode(node.DisplayName, node.FullPath, node.RelativePath, false, node.FileType, node.IsKnownFileType, node.DateModified, children);
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
                    file.FullPath,
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

                while (sharedPartCount < maxSharedParts && string.Equals(commonPathParts[sharedPartCount], pathParts[sharedPartCount], StringComparison.OrdinalIgnoreCase))
                    sharedPartCount++;

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
            return string.IsNullOrWhiteSpace(sanitizedFileName) ? "SelectedCode" : sanitizedFileName;
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

        private sealed record SourceSnapshot(DecodedBundleViewerWindow.SourceSnapshotState State, string Content);

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