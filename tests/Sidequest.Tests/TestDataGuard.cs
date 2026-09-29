using System.IO;
using System.Runtime.CompilerServices;
using Sidequest.Services;

namespace Sidequest.Tests;

/// <summary>
/// Repoints every store - Sidequests, settings and the WeeklyReminder import - away from
/// the user's real data before a single test runs.
///
/// This exists because the suite once did reach them: with tests running in parallel,
/// one test's cleanup restored the real %APPDATA% paths while another was mid-run, and
/// the migration renamed the live WeeklyReminder week.json and wrote a fixture into the
/// real Sidequest folder. Serialising the suite fixes that race, but it still leaves
/// TempStore restoring the real paths afterwards - one leak away from the same outcome.
///
/// With this initializer the defaults are already harmless, so TempStore saves and
/// restores a safe root and no code path in the test assembly can name a real folder.
/// </summary>
internal static class TestDataGuard
{
    internal static string SafeRoot { get; } =
        Path.Combine(Path.GetTempPath(), "sidequest-tests", "_guard");

    [ModuleInitializer]
    internal static void RedirectAwayFromRealData()
    {
        SidequestStore.Directory = Path.Combine(SafeRoot, "Sidequest");
        Storage.Directory = Path.Combine(SafeRoot, "Sidequest");
        LegacyMigration.LegacyDirectory = Path.Combine(SafeRoot, "WeeklyReminder");
    }
}
