using System.Windows;
using System.Windows.Input;

namespace NexusProgrammer;

public partial class ReplaceDialog : Window
{
    private readonly Func<string, bool, Task> _replaceAsync;

    public ReplaceDialog(string mode, Func<string, bool, Task> replaceAsync)
    {
        InitializeComponent();
        _replaceAsync = replaceAsync;
        HintText.Text = mode.Equals("Text", StringComparison.OrdinalIgnoreCase)
            ? "Replacement text"
            : "Replacement hex bytes";
        ReplaceTextBox.Focus();
    }

    private async void Replace_Click(object sender, RoutedEventArgs e)
    {
        await RunReplaceAsync(replaceAll: false);
    }

    private async void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        await RunReplaceAsync(replaceAll: true);
    }

    private void ReplaceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        Replace_Click(sender, e);
    }

    private async Task RunReplaceAsync(bool replaceAll)
    {
        ReplaceButton.IsEnabled = false;
        ReplaceAllButton.IsEnabled = false;
        try
        {
            await _replaceAsync(ReplaceTextBox.Text, replaceAll);
        }
        finally
        {
            ReplaceButton.IsEnabled = true;
            ReplaceAllButton.IsEnabled = true;
            ReplaceTextBox.Focus();
        }
    }
}
