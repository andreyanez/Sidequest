using System;
using System.IO;
using Sidequest.Services;
using Xunit;

namespace Sidequest.Tests;

/// <summary>
/// Regression tests for the one incident this suite has caused: it migrated the user's
/// live WeeklyReminder data. These assert that no real folder is reachable, both at rest
/// and after a TempStore has come and gone.
/// </summary>
public class TestDataGuardTests
{
    private static string RealAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    [Fact]
    public void StoresNeverPointAtRealAppData()
    {
        Assert.StartsWith(Path.GetTempPath(), SidequestStore.Directory, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetTempPath(), LegacyMigration.LegacyDirectory, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(RealAppData, SidequestStore.Directory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RealAppData, LegacyMigration.LegacyDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TempStoreRestoresToTheSafeRoot_NotToRealAppData()
    {
        using (var temp = new TempStore())
        {
            Assert.Equal(temp.StoreDirectory, SidequestStore.Directory);
        }

        Assert.DoesNotContain(RealAppData, SidequestStore.Directory, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RealAppData, LegacyMigration.LegacyDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
