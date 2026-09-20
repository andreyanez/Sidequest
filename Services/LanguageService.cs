using System.Globalization;
using System.Threading;
using System.Windows;
using Sidequest.Models;

namespace Sidequest.Services;

/// <summary>
/// Swaps the active string dictionary at runtime, mirroring <see cref="ThemeService"/>.
/// Slot 1 of the application's merged dictionaries is reserved for it (slot 0 is the
/// theme), so every DynamicResource string updates without reopening windows.
///
/// Text built in code cannot update itself, so <see cref="Changed"/> tells windows to
/// re-apply theirs.
/// </summary>
public static class LanguageService
{
    private const int DictionarySlot = 1;

    public static AppLanguage Current { get; private set; } = AppLanguage.English;

    /// <summary>Raised after a switch, for text that code sets rather than XAML.</summary>
    public static event Action? Changed;

    public static void Apply(AppLanguage language)
    {
        Current = language;

        // Also drives date and day-name formatting, which would otherwise stay on the
        // Windows locale and leave English text next to Spanish weekdays.
        var culture = CultureInfo.GetCultureInfo(language == AppLanguage.Spanish ? "es-ES" : "en-GB");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var source = language == AppLanguage.Spanish ? "Strings/Spanish.xaml" : "Strings/English.xaml";
        var dictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (merged.Count > DictionarySlot) merged[DictionarySlot] = dictionary;
        else merged.Add(dictionary);

        Changed?.Invoke();
    }

    /// <summary>The string for a key, or the key itself if it is missing from a dictionary.</summary>
    public static string Get(string key) =>
        System.Windows.Application.Current.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>First-run guess: Spanish if Windows is Spanish, otherwise English.</summary>
    public static AppLanguage DetectFromSystem() =>
        CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "es"
            ? AppLanguage.Spanish
            : AppLanguage.English;
}
