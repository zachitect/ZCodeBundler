using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZCodeBundler.Decoding;

namespace ZCodeBundler
{
    public partial class DecodedBundleViewerWindow : Window
    {
        public enum SourceSnapshotState
        {
            Exists,
            Missing,
            InvalidPath,
            SourceReadError
        }

        private const int MaxAlignedDiffLineCount = 1000;
        private static readonly Brush NormalBackground = Brushes.Transparent;
        private static readonly Brush RemovedBackground = new SolidColorBrush(Color.FromRgb(255, 230, 230));
        private static readonly Brush AddedBackground = new SolidColorBrush(Color.FromRgb(232, 248, 237));

        private DiffViewModel diffViewModel;
        private int currentDiffBlockIndex = -1;

        public DecodedBundleViewerWindow(
            DecodedBundleListItem decodedFile,
            SourceSnapshotState sourceSnapshotState,
            string sourceContent,
            string statusText,
            Action<DecodedBundleViewerWindow> applyRequested)
        {
            InitializeComponent();

            DecodedFile = decodedFile;
            OpenedSourceState = sourceSnapshotState;
            OpenedSourceContent = sourceSnapshotState == SourceSnapshotState.Exists ? sourceContent : string.Empty;
            ApplyRequested = applyRequested;

            var titleFileName = string.IsNullOrWhiteSpace(decodedFile.FileName) ? decodedFile.SourceFileName : decodedFile.FileName;
            Title = $"ZDecoder - {titleFileName}";

            Refresh(sourceContent, statusText, decodedFile.Status);
        }

        public DecodedBundleListItem DecodedFile { get; }
        public SourceSnapshotState OpenedSourceState { get; }
        public string OpenedSourceContent { get; }
        public DecodedSourceStatus CurrentStatus { get; private set; }
        private Action<DecodedBundleViewerWindow> ApplyRequested { get; }

        public void Refresh(string sourceContent, string statusText, DecodedSourceStatus status)
        {
            CurrentStatus = status;
            StatusTextBlock.Text = statusText;
            TargetPathTextBlock.Text = DecodedFile.Path;

            diffViewModel = new DiffViewModel(BuildDiffRows(sourceContent, DecodedFile.Content));
            currentDiffBlockIndex = -1;
            DataContext = diffViewModel;

            ApplyChangesButton.IsEnabled = status is DecodedSourceStatus.Different or DecodedSourceStatus.Missing or DecodedSourceStatus.DuplicateTarget;
            UpdateDiffNavigationButtons();
        }

        private void ApplyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyRequested(this);
        }

        private void PreviousDiffButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToDiff(-1);
        }

        private void NextDiffButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToDiff(1);
        }

        private void JumpToDiff(int direction)
        {
            if (diffViewModel == null || diffViewModel.DiffBlockStartIndexes.Count == 0)
                return;

            if (currentDiffBlockIndex < 0)
            {
                currentDiffBlockIndex = direction > 0 ? 0 : diffViewModel.DiffBlockStartIndexes.Count - 1;
            }
            else
            {
                currentDiffBlockIndex = (currentDiffBlockIndex + direction + diffViewModel.DiffBlockStartIndexes.Count) % diffViewModel.DiffBlockStartIndexes.Count;
            }

            var rowIndex = diffViewModel.DiffBlockStartIndexes[currentDiffBlockIndex];
            SelectAndScrollToRow(rowIndex);
        }

        private void SelectAndScrollToRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= DiffRowsDataGrid.Items.Count)
                return;

            DiffRowsDataGrid.SelectedIndex = rowIndex;
            DiffRowsDataGrid.ScrollIntoView(DiffRowsDataGrid.Items[rowIndex]);
            DiffRowsDataGrid.UpdateLayout();

            var row = DiffRowsDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
            row?.BringIntoView();
            DiffRowsDataGrid.Focus();
        }

        private void UpdateDiffNavigationButtons()
        {
            var hasDiffBlocks = diffViewModel != null && diffViewModel.DiffBlockStartIndexes.Count > 0;
            PreviousDiffButton.IsEnabled = hasDiffBlocks;
            NextDiffButton.IsEnabled = hasDiffBlocks;
        }

        private static List<DiffRow> BuildDiffRows(string sourceContent, string decodedContent)
        {
            var sourceLines = SplitLines(sourceContent);
            var decodedLines = SplitLines(decodedContent);

            if (string.Equals(sourceContent, decodedContent, StringComparison.Ordinal))
                return BuildMatchingRows(sourceLines);

            if (HasOnlyLineEndingDifferences(sourceContent, decodedContent))
                return BuildLineEndingOnlyDifferenceRows(sourceContent, decodedContent, sourceLines, decodedLines);

            if (sourceLines.Count + decodedLines.Count > MaxAlignedDiffLineCount)
                return BuildLargeDiffPreviewRows(sourceLines.Count, decodedLines.Count);

            var operations = BuildDiffOperations(sourceLines, decodedLines);
            var rows = new List<DiffRow>();
            var sourceLineNumber = 1;
            var decodedLineNumber = 1;
            var index = 0;

            while (index < operations.Count)
            {
                if (operations[index].Kind == DiffOperationKind.Unchanged)
                {
                    rows.Add(DiffRow.Unchanged(sourceLineNumber, operations[index].Text, decodedLineNumber, operations[index].Text));
                    sourceLineNumber++;
                    decodedLineNumber++;
                    index++;
                    continue;
                }

                var removedLines = new List<string>();
                var addedLines = new List<string>();

                while (index < operations.Count && operations[index].Kind != DiffOperationKind.Unchanged)
                {
                    if (operations[index].Kind == DiffOperationKind.Removed)
                        removedLines.Add(operations[index].Text);
                    else
                        addedLines.Add(operations[index].Text);

                    index++;
                }

                var pairedCount = Math.Min(removedLines.Count, addedLines.Count);

                for (var pairIndex = 0; pairIndex < pairedCount; pairIndex++)
                {
                    rows.Add(DiffRow.Changed(sourceLineNumber, removedLines[pairIndex], decodedLineNumber, addedLines[pairIndex]));
                    sourceLineNumber++;
                    decodedLineNumber++;
                }

                for (var removedIndex = pairedCount; removedIndex < removedLines.Count; removedIndex++)
                {
                    rows.Add(DiffRow.Removed(sourceLineNumber, removedLines[removedIndex]));
                    sourceLineNumber++;
                }

                for (var addedIndex = pairedCount; addedIndex < addedLines.Count; addedIndex++)
                {
                    rows.Add(DiffRow.Added(decodedLineNumber, addedLines[addedIndex]));
                    decodedLineNumber++;
                }
            }

            return rows;
        }

        private static List<DiffRow> BuildMatchingRows(List<string> lines)
        {
            var rows = new List<DiffRow>();

            for (var index = 0; index < lines.Count; index++)
                rows.Add(DiffRow.Unchanged(index + 1, lines[index], index + 1, lines[index]));

            return rows;
        }

        private static List<DiffRow> BuildLineEndingOnlyDifferenceRows(
            string sourceContent,
            string decodedContent,
            List<string> sourceLines,
            List<string> decodedLines)
        {
            var rows = new List<DiffRow>
            {
                DiffRow.Changed(
                    0,
                    $"Line endings differ: source uses {GetLineEndingDescription(sourceContent)}.",
                    0,
                    $"Decoded uses {GetLineEndingDescription(decodedContent)}. Applying writes decoded line endings exactly.")
            };

            var matchingLineCount = Math.Min(sourceLines.Count, decodedLines.Count);

            for (var index = 0; index < matchingLineCount; index++)
                rows.Add(DiffRow.Unchanged(index + 1, sourceLines[index], index + 1, decodedLines[index]));

            return rows;
        }

        private static List<DiffRow> BuildLargeDiffPreviewRows(int sourceLineCount, int decodedLineCount)
        {
            return new List<DiffRow>
            {
                DiffRow.Changed(
                    0,
                    $"Diff is too large to display safely/usefully. Source lines: {sourceLineCount}.",
                    0,
                    $"Decoded lines: {decodedLineCount}. Apply still writes decoded bundle content exactly.")
            };
        }

        private static List<DiffOperation> BuildDiffOperations(List<string> sourceLines, List<string> decodedLines)
        {
            var sourceCount = sourceLines.Count;
            var decodedCount = decodedLines.Count;
            var maxEditCount = sourceCount + decodedCount;
            var offset = maxEditCount + 1;
            var vector = new int[(maxEditCount * 2) + 3];
            var trace = new List<int[]>();
            vector[offset + 1] = 0;

            for (var editCount = 0; editCount <= maxEditCount; editCount++)
            {
                trace.Add((int[])vector.Clone());

                for (var diagonal = -editCount; diagonal <= editCount; diagonal += 2)
                {
                    int sourceIndex;

                    if (diagonal == -editCount || diagonal != editCount && vector[offset + diagonal - 1] < vector[offset + diagonal + 1])
                    {
                        sourceIndex = vector[offset + diagonal + 1];
                    }
                    else
                    {
                        sourceIndex = vector[offset + diagonal - 1] + 1;
                    }

                    var decodedIndex = sourceIndex - diagonal;

                    while (sourceIndex < sourceCount && decodedIndex < decodedCount && sourceLines[sourceIndex] == decodedLines[decodedIndex])
                    {
                        sourceIndex++;
                        decodedIndex++;
                    }

                    vector[offset + diagonal] = sourceIndex;

                    if (sourceIndex >= sourceCount && decodedIndex >= decodedCount)
                        return BacktrackDiffOperations(sourceLines, decodedLines, trace, editCount, offset);
                }
            }

            return new List<DiffOperation>();
        }

        private static List<DiffOperation> BacktrackDiffOperations(
            List<string> sourceLines,
            List<string> decodedLines,
            List<int[]> trace,
            int editCount,
            int offset)
        {
            var operations = new List<DiffOperation>();
            var sourceIndex = sourceLines.Count;
            var decodedIndex = decodedLines.Count;

            for (var currentEditCount = editCount; currentEditCount >= 0; currentEditCount--)
            {
                var vector = trace[currentEditCount];
                var diagonal = sourceIndex - decodedIndex;

                if (currentEditCount == 0)
                {
                    while (sourceIndex > 0 && decodedIndex > 0)
                    {
                        operations.Add(new DiffOperation(DiffOperationKind.Unchanged, sourceLines[sourceIndex - 1]));
                        sourceIndex--;
                        decodedIndex--;
                    }

                    break;
                }

                var previousDiagonal = diagonal == -currentEditCount
                    || diagonal != currentEditCount && vector[offset + diagonal - 1] < vector[offset + diagonal + 1]
                    ? diagonal + 1
                    : diagonal - 1;

                var previousSourceIndex = vector[offset + previousDiagonal];
                var previousDecodedIndex = previousSourceIndex - previousDiagonal;

                while (sourceIndex > previousSourceIndex && decodedIndex > previousDecodedIndex)
                {
                    operations.Add(new DiffOperation(DiffOperationKind.Unchanged, sourceLines[sourceIndex - 1]));
                    sourceIndex--;
                    decodedIndex--;
                }

                if (sourceIndex == previousSourceIndex)
                {
                    operations.Add(new DiffOperation(DiffOperationKind.Added, decodedLines[decodedIndex - 1]));
                    decodedIndex--;
                }
                else
                {
                    operations.Add(new DiffOperation(DiffOperationKind.Removed, sourceLines[sourceIndex - 1]));
                    sourceIndex--;
                }
            }

            operations.Reverse();
            return operations;
        }

        private static List<string> SplitLines(string content)
        {
            if (content.Length == 0)
                return new List<string>();

            return NormalizeLineEndings(content)
                .Split('\n')
                .ToList();
        }

        private static bool HasOnlyLineEndingDifferences(string sourceContent, string decodedContent)
        {
            return string.Equals(NormalizeLineEndings(sourceContent), NormalizeLineEndings(decodedContent), StringComparison.Ordinal);
        }

        private static string NormalizeLineEndings(string content)
        {
            return content
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
        }

        private static string GetLineEndingDescription(string content)
        {
            var hasCrLf = false;
            var hasLf = false;
            var hasCr = false;

            for (var index = 0; index < content.Length; index++)
            {
                if (content[index] == '\r')
                {
                    if (index + 1 < content.Length && content[index + 1] == '\n')
                    {
                        hasCrLf = true;
                        index++;
                    }
                    else
                    {
                        hasCr = true;
                    }

                    continue;
                }

                if (content[index] == '\n')
                    hasLf = true;
            }

            var lineEndingNames = new List<string>();
            if (hasCrLf)
                lineEndingNames.Add("CRLF");
            if (hasLf)
                lineEndingNames.Add("LF");
            if (hasCr)
                lineEndingNames.Add("CR");

            return lineEndingNames.Count == 0 ? "no line endings" : string.Join(" + ", lineEndingNames);
        }

        public sealed class DiffViewModel
        {
            public DiffViewModel(List<DiffRow> diffRows)
            {
                DiffRows = diffRows;
                DiffBlockStartIndexes = BuildDiffBlockStartIndexes(diffRows);
            }

            public List<DiffRow> DiffRows { get; }
            public List<int> DiffBlockStartIndexes { get; }

            private static List<int> BuildDiffBlockStartIndexes(List<DiffRow> diffRows)
            {
                var diffBlockStartIndexes = new List<int>();

                for (var index = 0; index < diffRows.Count; index++)
                {
                    if (!diffRows[index].IsDifferent)
                        continue;

                    if (index == 0 || !diffRows[index - 1].IsDifferent)
                        diffBlockStartIndexes.Add(index);
                }

                return diffBlockStartIndexes;
            }
        }

        public sealed class DiffRow
        {
            private DiffRow(
                string sourceLineNumberText,
                string sourceText,
                string decodedLineNumberText,
                string decodedText,
                Brush sourceBackground,
                Brush decodedBackground,
                bool isDifferent)
            {
                SourceLineNumberText = sourceLineNumberText;
                SourceText = sourceText;
                DecodedLineNumberText = decodedLineNumberText;
                DecodedText = decodedText;
                SourceBackground = sourceBackground;
                DecodedBackground = decodedBackground;
                IsDifferent = isDifferent;
            }

            public string SourceLineNumberText { get; }
            public string SourceText { get; }
            public string DecodedLineNumberText { get; }
            public string DecodedText { get; }
            public Brush SourceBackground { get; }
            public Brush DecodedBackground { get; }
            public bool IsDifferent { get; }

            public static DiffRow Unchanged(int sourceLineNumber, string sourceText, int decodedLineNumber, string decodedText)
            {
                return new DiffRow(
                    sourceLineNumber > 0 ? sourceLineNumber.ToString() : string.Empty,
                    sourceText,
                    decodedLineNumber > 0 ? decodedLineNumber.ToString() : string.Empty,
                    decodedText,
                    NormalBackground,
                    NormalBackground,
                    false);
            }

            public static DiffRow Changed(int sourceLineNumber, string sourceText, int decodedLineNumber, string decodedText)
            {
                return new DiffRow(
                    sourceLineNumber > 0 ? sourceLineNumber.ToString() : string.Empty,
                    sourceText,
                    decodedLineNumber > 0 ? decodedLineNumber.ToString() : string.Empty,
                    decodedText,
                    RemovedBackground,
                    AddedBackground,
                    true);
            }

            public static DiffRow Removed(int sourceLineNumber, string sourceText)
            {
                return new DiffRow(sourceLineNumber.ToString(), sourceText, string.Empty, string.Empty, RemovedBackground, NormalBackground, true);
            }

            public static DiffRow Added(int decodedLineNumber, string decodedText)
            {
                return new DiffRow(string.Empty, string.Empty, decodedLineNumber.ToString(), decodedText, NormalBackground, AddedBackground, true);
            }
        }

        private sealed class DiffOperation
        {
            public DiffOperation(DiffOperationKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }

            public DiffOperationKind Kind { get; }
            public string Text { get; }
        }

        private enum DiffOperationKind
        {
            Unchanged,
            Removed,
            Added
        }
    }
}