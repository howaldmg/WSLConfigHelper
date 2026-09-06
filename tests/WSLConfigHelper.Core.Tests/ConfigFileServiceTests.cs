using WSLConfigHelper.Core.Parsing;
using WSLConfigHelper.Core.Storage;
using Xunit;

namespace WSLConfigHelper.Core.Tests;

public class ConfigFileServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _configPath;

    public ConfigFileServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WSLConfigHelperTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _configPath = Path.Combine(_tempDirectory, ".wslconfig");
    }

    [Fact]
    public void CreatesSingleBackupOnSaveAndRestoresCorrectly()
    {
        var service = new WslConfigFileService(_configPath);

        // 1. Initial write
        var doc1 = new IniDocument();
        doc1.SetValue("wsl2", "memory", "8GB");
        service.Save(doc1);

        Assert.True(service.Exists());
        Assert.False(service.BackupExists()); // No previous file existed before first save

        // 2. Second write (should backup doc1)
        var doc2 = new IniDocument();
        doc2.SetValue("wsl2", "memory", "16GB");
        service.Save(doc2);

        Assert.True(service.BackupExists());
        var loadedDoc2 = service.Load();
        Assert.Equal("16GB", loadedDoc2.GetValue("wsl2", "memory"));

        // Verify backup contents
        var backupText = File.ReadAllText(service.BackupFilePath);
        Assert.Contains("memory=8GB", backupText);

        // 3. Third write (should overwrite .bak with doc2, not create .bak.1)
        var doc3 = new IniDocument();
        doc3.SetValue("wsl2", "memory", "32GB");
        service.Save(doc3);

        var filesInDir = Directory.GetFiles(_tempDirectory);
        Assert.Equal(2, filesInDir.Length); // Only .wslconfig and .wslconfig.bak
        Assert.Contains(service.ConfigFilePath, filesInDir);
        Assert.Contains(service.BackupFilePath, filesInDir);

        var newBackupText = File.ReadAllText(service.BackupFilePath);
        Assert.Contains("memory=16GB", newBackupText);

        // 4. Test Restore
        var restored = service.RestoreBackup();
        Assert.True(restored);

        var restoredDoc = service.Load();
        Assert.Equal("16GB", restoredDoc.GetValue("wsl2", "memory"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch { /* best effort cleanup */ }
    }
}
