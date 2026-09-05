using System.Windows;
using WeeklyReminder.Services;

namespace WeeklyReminder.Views;

/// <summary>
/// Small modal confirmation styled like the rest of the app, rather than a system
/// MessageBox. Used for destructive actions only.
/// </summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string heading, string detail, string? confirmLabel, string footnote)
    {
        InitializeComponent();

        HeadingText.Text = heading;
        DetailText.Text = detail;
        ConfirmButton.Content = confirmLabel ?? LanguageService.Get("L_Remove");

        FooterNote.Text = footnote;
        FooterNote.Visibility = string.IsNullOrEmpty(footnote) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows the dialog modally over <paramref name="owner"/>. True if confirmed.</summary>
    public static bool Ask(
        Window owner,
        string heading,
        string detail,
        string? confirmLabel = null,
        string footnote = "")
    {
        var dialog = new ConfirmDialog(heading, detail, confirmLabel, footnote) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
