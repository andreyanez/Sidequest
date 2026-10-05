using System.Globalization;
using System.Windows;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using Button = System.Windows.Controls.Button;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Sidequest.Views;

/// <summary>
/// The Sidequests list (5.4), with the new-Sidequest form (5.5) opening in the footer in
/// place of the dashed button.
/// </summary>
public partial class SidequestsView : UserControl
{
    private readonly MainWindow _shell;
    private readonly SidequestProject? _focusOnLoad;
    private readonly Action _refresh;

    /// <param name="focus">Put keyboard focus on this Sidequest's button once shown.</param>
    public SidequestsView(MainWindow shell, SidequestProject? focus = null)
    {
        InitializeComponent();
        _shell = shell;
        _focusOnLoad = focus;

        _refresh = Refresh;
        Loaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
            ThemeService.Changed += _refresh;
            LanguageService.Changed += _refresh;
            Refresh();

            if (_focusOnLoad is not null) FocusSidequest(_focusOnLoad);
        };
        Unloaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
        };
    }

    private bool FormOpen => Form.Visibility == Visibility.Visible;

    private SidequestTheme SelectedTheme =>
        (ThemePicker.SelectedItem as ThemeSwatch)?.Theme ?? SidequestTheme.Amber;

    /// <summary>
    /// Rebuilds everything resolved in code: the buttons carry per-Sidequest colours that
    /// depend on the app theme, and their counts and the form's label are built from strings.
    /// </summary>
    private void Refresh()
    {
        RebuildRows();

        if (FormOpen)
        {
            var theme = SelectedTheme;
            ThemePicker.ItemsSource = ThemeSwatch.All();
            SelectTheme(theme);
            ApplyFormTheme(theme);
        }
    }

    private void RebuildRows() =>
        ProjectList.ItemsSource = App.Current.Projects
            .OrderBy(p => p.SortIndex)
            .Select(p => new SidequestButtonRow(p))
            .ToList();

    private void Sidequest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SidequestButtonRow row }) return;

        _shell.ShowSidequest(row.Project);
    }

    private void FocusSidequest(SidequestProject project)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var button = VisualTree.FindDescendant<Button>(
                ProjectList, b => b.DataContext is SidequestButtonRow row && row.Project == project);
            button?.Focus();
        });
    }

    // ---- New Sidequest (5.5) ------------------------------------------------------

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var theme = SidequestRules.DefaultTheme(App.Current.Projects);

        // 5.5: the form takes the button's place in the footer, and the list keeps its
        // buttons as they are, with less room to scroll in.
        NewButton.Visibility = Visibility.Collapsed;
        Form.Visibility = Visibility.Visible;

        NameBox.Text = string.Empty;
        ThemePicker.ItemsSource = ThemeSwatch.All();
        SelectTheme(theme);
        ApplyFormTheme(theme);
        CreateButton.IsEnabled = false;

        // 5.5: the name field takes focus.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => NameBox.Focus());
    }

    private void CloseForm()
    {
        Form.Visibility = Visibility.Collapsed;
        NewButton.Visibility = Visibility.Visible;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => NewButton.Focus());
    }

    private void SelectTheme(SidequestTheme theme) =>
        ThemePicker.SelectedItem = ThemePicker.Items.Cast<ThemeSwatch>().First(s => s.Theme == theme);

    /// <summary>The form wears the theme being chosen, as FnSidequestNuevo shows.</summary>
    private void ApplyFormTheme(SidequestTheme theme)
    {
        var app = ThemeService.Current;
        var colour = SidequestPalette.Brush(theme, app);

        FormFill.Background = SidequestPalette.Brush(theme, app, SidequestPalette.Alpha.FormTint);
        FormEdge.BorderBrush = SidequestPalette.Brush(theme, app, SidequestPalette.Alpha.FormBorder);
        FormLabel.Foreground = colour;
        NameBox.Tag = colour;

        // A section label, so upper case (4.1); CSS does it in the artboard.
        FormLabel.Text = LanguageService.Get("L_NewSidequest").ToUpper(CultureInfo.CurrentCulture);
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemePicker.SelectedItem is ThemeSwatch swatch) ApplyFormTheme(swatch.Theme);
    }

    /// <summary>5.5: creating needs a name that is not blank.</summary>
    private void NameBox_TextChanged(object sender, TextChangedEventArgs e) =>
        CreateButton.IsEnabled = NameBox.Text.Trim().Length > 0;

    private void Form_KeyDown(object sender, KeyEventArgs e)
    {
        // A focused button handles its own Enter, so this only sees Enter from the field
        // and the swatches - which is where 5.5's "Enter crea" applies.
        if (e.Key == Key.Enter)
        {
            Create();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseForm();
            e.Handled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseForm();

    private void Create_Click(object sender, RoutedEventArgs e) => Create();

    /// <summary>Creates the Sidequest and goes straight to it, empty, to add its items (5.5).</summary>
    private void Create()
    {
        if (NameBox.Text.Trim().Length == 0) return;

        var project = SidequestRules.Create(App.Current.Projects, NameBox.Text, SelectedTheme);
        SidequestStore.Save(App.Current.Projects);

        _shell.ShowSidequest(project, justCreated: true);
    }
}
