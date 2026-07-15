using System.Windows;

namespace ZCodeBundler.Dialogs;

internal partial class MessageDialog : Window
{
    internal MessageDialog(string title, string message, string affirmativeLabel = "Yes", MessageBoxImage icon = MessageBoxImage.Information)
    {
        InitializeComponent();
        Title = title;
        TextInstruction.Text = message;
        ButtonOptionA.Content = affirmativeLabel;
        TextIcon.Text = GetIconText(icon);
        SizeToContent = SizeToContent.Height;
    }

    private static string GetIconText(MessageBoxImage icon)
    {
        return icon switch
        {
            MessageBoxImage.None => string.Empty,
            MessageBoxImage.Error => "❌",
            MessageBoxImage.Question => "❓",
            MessageBoxImage.Warning => "⚠",
            _ => "ℹ"
        };
    }

    private void ButtonOptionA_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}