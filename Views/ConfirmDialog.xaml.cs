using System.Windows;

namespace Sidequest.Views;

/// <summary>
/// Small modal confirmation styled like the rest of the app, rather than a system
/// MessageBox. Used for destructive actions only. Enter confirms and Escape cancels.
/// </summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string heading, string detail, string confirmLabel, string footnote)
    {
        InitializeComponent();

        HeadingText.Text = heading;
        DetailText.Text = detail;
        ConfirmButton.Content = confirmLabel;

        // Some questions carry their subject in the heading itself, and need no box.
        DetailBox.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;

        FooterNote.Text = footnote;
        FooterNote.Visibility = string.IsNullOrEmpty(footnote) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows the dialog modally over <paramref name="owner"/>. True if confirmed.</summary>
    /// <param name="confirmLabel">Names the action, as 4.4's buttons do ("Eliminar item").</param>
    public static bool Ask(
        Window owner,
        string heading,
        string detail,
        string confirmLabel,
        string footnote = "")
    {
        var dialog = new ConfirmDialog(heading, detail, confirmLabel, footnote) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
