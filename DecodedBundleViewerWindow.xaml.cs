using System.Windows;
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

        private const int MaxAlignedDiffLineCount = 5000;

        private static readonly Brush NormalBackground = Brushes.Transparent;
        private static readonly Brush RemovedBackground = new SolidColorBrush(Color.FromRgb(255, 230, 230));
        private static readonly Brush AddedBackground = new SolidColorBrush(Color.FromRgb(232, 248, 237));

        public DecodedBundleViewerWindow(
            DecodedBundleListItem decodedFile,
            SourceSnapshotState sourceSnapshotState,
            string sourceHeader,
            string sourceContent,
            string decodedHeader,
            string statusText,
            Action<DecodedBundleViewerWindow> applyRequested)
        {
            InitializeComponent();

            DecodedFile = decodedFile;
            OpenedSourceState = sourceSnapshotState;
            OpenedSourceContent = sourceSnapshotState == SourceSnapshotState.Exists ? sourceContent : string.Empty;
            ApplyRequested = applyRequested;

            Title = $"Decoded diff - {decodedFile.FileName}";
            Refresh(sourceHeader, sourceContent, statusText, decodedFile.Status);
        }

        public DecodedBundleListItem DecodedFile { get; }
        public SourceSnapshotState OpenedSourceState { get; }
        public string OpenedSourceContent { get; }
        public DecodedSourceStatus CurrentStatus { get; private set; }
        private Action<DecodedBundleViewerWindow> ApplyRequested { get; }

        public void Refresh(string sourceHeader, string sourceContent, string statusText, DecodedSourceStatus status)
        {
            CurrentStatus = status;
            StatusTextBlock.Text = statusText;
            TargetPathTextBlock.Text = DecodedFile.Path;
            DataContext = new DiffViewModel(BuildDiffRows(sourceContent, DecodedFile.Content));
            ApplyChangesButton.IsEnabled = status is DecodedSourceStatus.Different or DecodedSourceStatus.Missing or DecodedSourceStatus.DuplicateTarget;
        }

        private void ApplyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyRequested(this);
        }

        private static List<DiffRow> BuildDiffRows(string sourceContent, string decodedContent)
        {
            var sourceLines = SplitLines(sourceContent);
            var decodedLines = SplitLines(decodedContent);

            if (string.Equals(sourceContent, decodedContent, StringComparison.Ordinal))
            {
                if (sourceLines.Count > MaxAlignedDiffLineCount)
                    return BuildLargeUnchangedRows(sourceLines.Count);

                return BuildMatchingRows(sourceLines);
            }

            if (sourceLines.Count + decodedLines.Count > MaxAlignedDiffLineCount)
                return BuildLargeDiffOmittedRows(sourceLines.Count, decodedLines.Count);

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

        private static List<DiffRow> BuildLargeUnchangedRows(int lineCount)
        {
            return new List<DiffRow>
            {
                DiffRow.Unchanged(
                    0,
                    $"Unchanged file preview omitted to keep the viewer responsive. Source lines: {lineCount}.",
                    0,
                    "Decoded content matches source content exactly.")
            };
        }

        private static List<DiffRow> BuildLargeDiffOmittedRows(int sourceLineCount, int decodedLineCount)
        {
            return new List<DiffRow>
            {
                DiffRow.Changed(
                    0,
                    $"Aligned diff omitted to keep the viewer responsive. Source lines: {sourceLineCount}.",
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

                    if (diagonal == -editCount
                        || diagonal != editCount && vector[offset + diagonal - 1] < vector[offset + diagonal + 1])
                    {
                        sourceIndex = vector[offset + diagonal + 1];
                    }
                    else
                    {
                        sourceIndex = vector[offset + diagonal - 1] + 1;
                    }

                    var decodedIndex = sourceIndex - diagonal;

                    while (sourceIndex < sourceCount
                        && decodedIndex < decodedCount
                        && sourceLines[sourceIndex] == decodedLines[decodedIndex])
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

            return content
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .ToList();
        }

        public sealed class DiffViewModel
        {
            public DiffViewModel(List<DiffRow> diffRows)
            {
                DiffRows = diffRows;
            }

            public List<DiffRow> DiffRows { get; }
        }

        public sealed class DiffRow
        {
            private DiffRow(
                string sourceLineNumberText,
                string sourceText,
                string decodedLineNumberText,
                string decodedText,
                Brush sourceBackground,
                Brush decodedBackground)
            {
                SourceLineNumberText = sourceLineNumberText;
                SourceText = sourceText;
                DecodedLineNumberText = decodedLineNumberText;
                DecodedText = decodedText;
                SourceBackground = sourceBackground;
                DecodedBackground = decodedBackground;
            }

            public string SourceLineNumberText { get; }
            public string SourceText { get; }
            public string DecodedLineNumberText { get; }
            public string DecodedText { get; }
            public Brush SourceBackground { get; }
            public Brush DecodedBackground { get; }

            public static DiffRow Unchanged(int sourceLineNumber, string sourceText, int decodedLineNumber, string decodedText)
            {
                return new DiffRow(
                    sourceLineNumber > 0 ? sourceLineNumber.ToString() : string.Empty,
                    sourceText,
                    decodedLineNumber > 0 ? decodedLineNumber.ToString() : string.Empty,
                    decodedText,
                    NormalBackground,
                    NormalBackground);
            }

            public static DiffRow Changed(int sourceLineNumber, string sourceText, int decodedLineNumber, string decodedText)
            {
                return new DiffRow(
                    sourceLineNumber > 0 ? sourceLineNumber.ToString() : string.Empty,
                    sourceText,
                    decodedLineNumber > 0 ? decodedLineNumber.ToString() : string.Empty,
                    decodedText,
                    RemovedBackground,
                    AddedBackground);
            }

            public static DiffRow Removed(int sourceLineNumber, string sourceText)
            {
                return new DiffRow(sourceLineNumber.ToString(), sourceText, string.Empty, string.Empty, RemovedBackground, NormalBackground);
            }

            public static DiffRow Added(int decodedLineNumber, string decodedText)
            {
                return new DiffRow(string.Empty, string.Empty, decodedLineNumber.ToString(), decodedText, NormalBackground, AddedBackground);
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