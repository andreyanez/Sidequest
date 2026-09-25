using System;
using System.IO;
using Sidequest.Services;

namespace Sidequest.Tests;

/// <summary>
/// Redirects both stores at a throwaway folder for the life of a test and puts them back
/// afterwards. Every test that touches storage must use this: the defaults point at the
/// user's real %APPDATA%, and the migration renames files there.
/// </summary>
public sealed class TempStore : IDisposable
{
    private readonly string _previousStore;
    private readonly string _previousLegacy;

    public string Root { get; }
    public string StoreDirectory { get; }
    public string LegacyDirectory { get; }

    public TempStore()
    {
        Root = Path.Combine(Path.GetTempPath(), "sidequest-tests", Guid.NewGuid().ToString("N"));
        StoreDirectory = Path.Combine(Root, "Sidequest");
        LegacyDirectory = Path.Combine(Root, "WeeklyReminder");

        Directory.CreateDirectory(StoreDirectory);
        Directory.CreateDirectory(LegacyDirectory);

        _previousStore = SidequestStore.Directory;
        _previousLegacy = LegacyMigration.LegacyDirectory;

        SidequestStore.Directory = StoreDirectory;
        LegacyMigration.LegacyDirectory = LegacyDirectory;
    }

    public void Dispose()
    {
        SidequestStore.Directory = _previousStore;
        LegacyMigration.LegacyDirectory = _previousLegacy;

        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { /* a leftover temp folder is not worth failing a test over */ }
    }
}
