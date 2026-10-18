using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using Sidequest.Models;
using Sidequest.Services;

// WinForms is referenced for NotifyIcon, so its implicit usings collide with WPF's.
using Button = System.Windows.Controls.Button;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DispatcherPriority = System.Windows.Threading.DispatcherPriority;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Point = System.Windows.Point;
using RectangleGeometry = System.Windows.Media.RectangleGeometry;
using RequestNavigateEventArgs = System.Windows.Navigation.RequestNavigateEventArgs;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using TextCompositionEventArgs = System.Windows.Input.TextCompositionEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace Sidequest.Views;

/// <summary>
/// A Sidequest's Notas tab (5.6): its notes and links, linked files and copied images, the
/// context its objectives are distilled from. In 0.1 this was an objective's own detail.
/// There is no save button - each change saves as it is made, and the notes when the field
/// is left or the tab closes.
/// </summary>
public partial class NotesTab : UserControl
{
    /// <summary>FnSidequestNotas's notes: 13px on a 1.55 line, blocks 6 apart.</summary>
    private const double NoteLineHeight = 13 * 1.55;
    private const double NoteGap = 6;

    private readonly SidequestProject _project;
    private readonly Action _refresh;

    public NotesTab(SidequestProject project)
    {
        InitializeComponent();
        _project = project;

        DataObject.AddPastingHandler(NotesBox, NotesBox_Pasting);

        _refresh = Refresh;
        Loaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;
            ThemeService.Changed += _refresh;
            LanguageService.Changed += _refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            ThemeService.Changed -= _refresh;
            LanguageService.Changed -= _refresh;

            // Switching tabs or leaving by the back button takes focus off the notes first,
            // which saves them (5.6); this is for the ways out that do not.
            SaveNotes();
        };
    }

    private static void Save() => SidequestStore.Save(App.Current.Projects);

    /// <summary>
    /// Rebuilds whatever is resolved in code: the labels, counters and link colours, which
    /// follow the language and the theme.
    /// </summary>
    private void Refresh()
    {
        // Section labels are upper case (4.1); CSS does it in the artboard.
        var culture = CultureInfo.CurrentCulture;
        NotesLabel.Text = LanguageService.Get("L_NotesAndLinks").ToUpper(culture);
        FilesLabel.Text = LanguageService.Get("L_LocalFiles").ToUpper(culture);
        ImagesLabel.Text = LanguageService.Get("L_Images").ToUpper(culture);

        LoadNotes();
        RefreshFiles();
        RefreshImages();
    }


    // ---- Notes and links ---------------------------------------------------------------

    /// <summary>
    /// The notes as a document: one paragraph per line, with every http(s) address a link in
    /// the Sidequest's colour. Rebuilt whenever the field is left, which is when 5.6 says
    /// new addresses are picked up.
    /// </summary>
    private void LoadNotes()
    {
        // The same document, refilled: a replaced one leaves the field's automation peer
        // reading the old text, which is what a screen reader would then announce.
        var document = NotesBox.Document;
        document.PagePadding = new Thickness(0);
        document.LineHeight = NoteLineHeight;
        document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        document.Blocks.Clear();

        var colour = SidequestPalette.Brush(_project.Theme, ThemeService.Current);
        foreach (var line in _project.Notes.Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, NoteGap) };
            foreach (var segment in NoteLinks.Split(line))
            {
                paragraph.Inlines.Add(segment.IsLink
                    ? new Hyperlink(new Run(segment.Text))
                    {
                        NavigateUri = new Uri(segment.Text),
                        Foreground = colour,
                        TextDecorations = TextDecorations.Underline,
                    }
                    : new Run(segment.Text));
            }

            document.Blocks.Add(paragraph);
        }

        UpdateNotesCounter();
    }

    /// <summary>The field's text as the model keeps it: lines joined by "\n".</summary>
    private string NotesText() => Plain(new TextRange(NotesBox.Document.ContentStart, NotesBox.Document.ContentEnd));

    /// <summary>
    /// A range's text with each paragraph break or line break as one "\n". TextRange gives
    /// "\r\n", and ends a whole document with one more, which is not part of the text.
    /// </summary>
    private string Plain(TextRange range)
    {
        var text = range.Text.Replace("\r\n", "\n");
        return range.End.CompareTo(NotesBox.Document.ContentEnd) == 0 && text.EndsWith('\n') ? text[..^1] : text;
    }

    /// <summary>How many more characters fit, counting the selection a keystroke replaces.</summary>
    private int Room() =>
        SidequestProject.MaxNotesLength - (NotesText().Length - Plain(new TextRange(NotesBox.Selection.Start, NotesBox.Selection.End)).Length);

    private void UpdateNotesCounter() =>
        NotesCounter.Text = LanguageService.Format("L_CounterFmt", NotesText().Length, SidequestProject.MaxNotesLength);

    private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var excess = NotesText().Length - SidequestProject.MaxNotesLength;
        if (excess > 0 && !_trimming) TrimBeforeCaret(excess);

        UpdateNotesCounter();
    }

    private bool _trimming;

    /// <summary>
    /// The cap's backstop. A RichTextBox queues typed characters and applies them in a
    /// batch, so the keystroke guard below can measure a document a few keys behind, and
    /// fast typing went five past the limit. Whatever lands over it is taken back off
    /// from just before the caret - the text that was just typed or pasted.
    /// </summary>
    private void TrimBeforeCaret(int count)
    {
        _trimming = true;
        try
        {
            var end = NotesBox.CaretPosition;
            var start = end;
            for (var i = 0; i < count; i++)
                start = start.GetNextInsertionPosition(LogicalDirection.Backward) ?? start;

            new TextRange(start, end).Text = string.Empty;
        }
        finally
        {
            _trimming = false;
        }
    }

    private void SaveNotes()
    {
        if (SidequestRules.SetNotes(_project, NotesText())) Save();
    }

    /// <summary>
    /// Leaving the field saves it and turns new addresses into links. Deferred, because
    /// focus can also move within the field (to a link) or into its Cut/Copy/Paste menu,
    /// and neither is leaving.
    /// </summary>
    private void NotesBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is MenuItem) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (NotesBox.IsKeyboardFocusWithin || !IsLoaded) return;

            SaveNotes();
            LoadNotes();
        });
    }

    // The notes are capped at MaxNotesLength characters. A RichTextBox has no MaxLength,
    // so typing, Enter and pasting are each held to the room that is left - a first line
    // that keeps the common case from flickering - and TrimBeforeCaret catches what gets
    // past it.

    private void NotesBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > Room()) e.Handled = true;
    }

    private void NotesBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        // Ctrl+Enter opens the address under the caret: the keyboard's Ctrl+click.
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenLinkAtCaret();
            e.Handled = true;
            return;
        }

        if (Room() < 1) e.Handled = true;
    }

    /// <summary>
    /// Pastes and drops arrive as plain text, cut to the room left: the notes are plain
    /// text (3), and formatting from another app would not survive the save anyway.
    /// </summary>
    private void NotesBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string
                   ?? e.DataObject.GetData(DataFormats.Text) as string;
        var room = Room();
        if (string.IsNullOrEmpty(text) || room <= 0)
        {
            e.CancelCommand();
            return;
        }

        text = text.Replace("\r\n", "\n");
        if (text.Length > room) text = text[..room];

        var plain = new DataObject();
        plain.SetData(DataFormats.UnicodeText, text);
        e.DataObject = plain;
    }

    /// <summary>5.6: Ctrl+click on a link opens it, which the field does for its own links.</summary>
    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Shell.OpenUrl(e.Uri.OriginalString);
        e.Handled = true;
    }

    /// <summary>5.6: while the field does not have focus, a plain click on a link opens it.</summary>
    private void NotesBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (NotesBox.IsKeyboardFocusWithin) return;
        if (LinkAt(e.GetPosition(NotesBox)) is not { } url) return;

        Shell.OpenUrl(url);
        e.Handled = true;
    }

    private string? LinkAt(Point point)
    {
        var position = NotesBox.GetPositionFromPoint(point, snapToText: false);
        for (var element = position?.Parent as TextElement; element is not null; element = element.Parent as TextElement)
        {
            if (element is Hyperlink { NavigateUri: { } uri }) return uri.OriginalString;
        }

        return null;
    }

    /// <summary>
    /// Works from the text, not the document's links, so an address typed a moment ago -
    /// not yet a link until the field is left - opens too.
    /// </summary>
    private void OpenLinkAtCaret()
    {
        var caret = NotesBox.CaretPosition;
        if (caret.Paragraph is not { } paragraph) return;

        var line = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        var offset = new TextRange(paragraph.ContentStart, caret).Text.Length;

        var at = 0;
        foreach (var segment in NoteLinks.Split(line))
        {
            if (segment.IsLink && offset >= at && offset <= at + segment.Text.Length)
            {
                Shell.OpenUrl(segment.Text);
                return;
            }

            at += segment.Text.Length;
        }
    }

    // ---- Local files ------------------------------------------------------------------------

    private void RefreshFiles()
    {
        FileRows.ItemsSource = _project.Files.Select(path => new FileRow(path)).ToList();
        FilesCounter.Text = LanguageService.Format("L_CounterFmt", _project.Files.Count, SidequestProject.MaxFiles);

        // 5.6: at five the button is disabled, and the counter says why.
        LinkFileButton.IsEnabled = _project.Files.Count < SidequestProject.MaxFiles;
    }

    private void FileListContent_SizeChanged(object sender, SizeChangedEventArgs e) =>
        FileListContent.Clip = new RectangleGeometry(new Rect(e.NewSize), 9, 9);

    /// <summary>5.6: a click opens the file in its own app. One that has gone stays put, greyed.</summary>
    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FileRow row }) return;
        if (!Shell.OpenFile(row.FullPath)) RefreshFiles();
    }

    private void Unlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FileRow row }) return;

        var index = _project.Files.IndexOf(row.FullPath);
        _project.Files.Remove(row.FullPath);
        Save();
        RefreshFiles();

        // Focus to the row that took its place, else the one before, else "Vincular".
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var rows = _project.Files;
            var next = rows.Count == 0 ? null : rows[Math.Clamp(index, 0, rows.Count - 1)];
            var button = next is null ? null : VisualTree.FindDescendant<Button>(FileRows,
                b => b.DataContext is FileRow r && r.FullPath == next && b.Style == FindResource("FileRowButton"));
            (button ?? LinkFileButton).Focus();
        });
    }

    private void LinkFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = LanguageService.Get("L_LinkFile"),
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        if (Attachments.LinkFiles(_project, dialog.FileNames) > 0)
        {
            Save();
            RefreshFiles();
        }
    }

    // ---- Images -------------------------------------------------------------------------

    /// <summary>Five cells: the images, then "+" if there is room, then empty placeholders.</summary>
    private void RefreshImages()
    {
        var slots = new List<ImageSlot>();
        for (var i = 0; i < SidequestProject.MaxImages; i++)
        {
            if (i < _project.Images.Count) slots.Add(new ImageThumb(_project.Images[i], i + 1));
            else if (i == _project.Images.Count) slots.Add(new AddImageSlot());
            else slots.Add(new EmptyImageSlot());
        }

        ImageList.ItemsSource = slots;
        ImagesCounter.Text = LanguageService.Format("L_CounterFmt", _project.Images.Count, SidequestProject.MaxImages);
    }

    private void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ImageThumb thumb })
            Shell.OpenFile(Attachments.MediaPath(thumb.Name));
    }

    private void RemoveImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ImageThumb thumb }) return;

        Attachments.RemoveImage(_project, thumb.Name, App.Current.Projects);
        Save();
        RefreshImages();

        // The "+" cell is where the next image goes, and it is always there after a removal.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            VisualTree.FindDescendant<Button>(ImageList, b => b.DataContext is AddImageSlot)?.Focus());
    }

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var extensions = string.Join(";", Attachments.ImageExtensions.Select(x => "*" + x));
        var dialog = new OpenFileDialog
        {
            Title = LanguageService.Get("L_AddImage"),
            Filter = $"{LanguageService.Get("L_Images")}|{extensions}",
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) AddImages(dialog.FileNames);
    }

    /// <summary>Copies images in (D4) until the Sidequest has five; the rest are left out.</summary>
    private void AddImages(IEnumerable<string> paths)
    {
        var added = paths.Count(path => Attachments.ImportImage(_project, path));
        if (added == 0) return;

        Save();
        RefreshImages();

        // The "+" cell that had focus is now a thumbnail. Focus follows "+" to its new
        // place, or, with all five in, goes to the last image.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            (VisualTree.FindDescendant<Button>(ImageList, b => b.DataContext is AddImageSlot)
             ?? VisualTree.FindDescendant<Button>(ImageList, b =>
                 b.DataContext is ImageThumb t && t.Name == _project.Images[^1] && b.Style == FindResource("ImageCellButton")))
            ?.Focus());
    }

    // 5.6: an image file dragged anywhere onto the tab is added too. Preview events, so
    // the notes field does not take it as text first; anything that is not image files is
    // left for the field to handle as usual.

    private static List<string> DroppedImages(DragEventArgs e) =>
        (e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>())
            .Where(Attachments.IsImage)
            .ToList();

    private void View_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var accepts = _project.Images.Count < SidequestProject.MaxImages && DroppedImages(e).Count > 0;
        e.Effects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        ShowDropTarget(accepts);
        e.Handled = true;
    }

    private void View_PreviewDragLeave(object sender, DragEventArgs e) => ShowDropTarget(false);

    private void View_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        ShowDropTarget(false);
        AddImages(DroppedImages(e));
        e.Handled = true;
    }

    /// <summary>While an image is dragged over the tab, the "+" cell shows where it lands.</summary>
    private void ShowDropTarget(bool on)
    {
        var add = VisualTree.FindDescendant<Button>(ImageList, b => b.DataContext is AddImageSlot);
        if (add is not null) add.Tag = on ? "DropTarget" : null;
    }
}
