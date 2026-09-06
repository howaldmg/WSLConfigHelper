using WSLConfigHelper.Core.Parsing;

namespace WSLConfigHelper.Core.Storage;

public class WslConfigFileService
{
    private readonly string _configFilePath;
    private readonly string _backupFilePath;

    public string ConfigFilePath => _configFilePath;
    public string BackupFilePath => _backupFilePath;

    public WslConfigFileService(string? customConfigFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customConfigFilePath))
        {
            _configFilePath = Path.GetFullPath(customConfigFilePath);
        }
        else
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _configFilePath = Path.Combine(userProfile, ".wslconfig");
        }

        _backupFilePath = _configFilePath + ".bak";
    }

    public bool Exists() => File.Exists(_configFilePath);

    public bool BackupExists() => File.Exists(_backupFilePath);

    public IniDocument Load()
    {
        if (!File.Exists(_configFilePath))
        {
            return new IniDocument();
        }

        var text = File.ReadAllText(_configFilePath);
        return IniDocument.Parse(text);
    }

    public void Save(IniDocument doc)
    {
        var targetDir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Maintain single backup policy: if .wslconfig exists, backup to .wslconfig.bak
        if (File.Exists(_configFilePath))
        {
            File.Copy(_configFilePath, _backupFilePath, overwrite: true);
        }

        var serialized = doc.Serialize();

        // Atomic write pattern: write to temporary file in same folder, then replace
        var tempFile = Path.Combine(targetDir ?? ".", $".wslconfig.tmp.{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(tempFile, serialized);
            File.Move(tempFile, _configFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* best effort */ }
            }
        }
    }

    public bool RestoreBackup()
    {
        if (!File.Exists(_backupFilePath))
        {
            return false;
        }

        // Atomic restore: copy .bak to target
        File.Copy(_backupFilePath, _configFilePath, overwrite: true);
        return true;
    }

    public DateTime? GetLastModified()
    {
        return File.Exists(_configFilePath) ? File.GetLastWriteTime(_configFilePath) : null;
    }

    public DateTime? GetBackupLastModified()
    {
        return File.Exists(_backupFilePath) ? File.GetLastWriteTime(_backupFilePath) : null;
    }
}
