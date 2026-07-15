using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZCodeBundler.Dialogs.TreeSelection;

internal sealed class TreeNodeViewModel : INotifyPropertyChanged
{
    private bool? _isSelected;
    private bool _isExpanded;

    internal bool? IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == null)
                value = false;

            SetIsSelected(value, true, true);
        }
    }

    internal bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    internal ObservableCollection<TreeNodeViewModel> Children { get; init; } = new();
    internal TreeNodeViewModel? Parent { get; set; }
    internal string DisplayName { get; init; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetIsSelected(bool? value, bool propagateToChildren, bool propagateToParent)
    {
        if (_isSelected == value)
            return;

        _isSelected = value;
        OnPropertyChanged(nameof(IsSelected));

        if (propagateToChildren && value.HasValue)
        {
            foreach (var child in Children)
                child.SetIsSelected(value, true, false);
        }

        if (propagateToParent)
            Parent?.UpdateSelectionFromChildren();
    }

    private void UpdateSelectionFromChildren()
    {
        if (Children.Count == 0)
            return;

        var selectedCount = Children.Count(child => child.IsSelected == true);
        var unselectedCount = Children.Count(child => child.IsSelected == false);
        bool? newValue = selectedCount == Children.Count ? true : unselectedCount == Children.Count ? false : null;

        if (_isSelected == newValue)
            return;

        _isSelected = newValue;
        OnPropertyChanged(nameof(IsSelected));
        Parent?.UpdateSelectionFromChildren();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}