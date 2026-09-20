using System.IO.Compression;

namespace StarStringAutoUpdater.Services;

public sealed record InstallResult(string GlobalIniPath, bool UserCfgWritten, bool GlobalIniBackedUp);

/// <summary>
/// Extracts global.ini (and, only if the user doesn't already have one, USER.cfg)
/// from a StarStrings-LIVE.zip and places them into the user's Star Citizen LIVE
/// folder, matching the layout documented in the StarStrings README:
///   LIVE\data\Localization\english\global.ini
///   LIVE\user.cfg
/// </summary>
public static class ZipInstaller
{
    private const string GlobalIniSuffix = "data/localization/english/global.ini";

    public static InstallResult Install(string zipPath, string liveFolderPath)
    {
        if (!Directory.Exists(liveFolderPath))
        {
            throw new DirectoryNotFoundException($"Star Citizen LIVE folder not found: {liveFolderPath}");
        }

        using var archive = ZipFile.OpenRead(zipPath);

        var globalIniEntry = archive.Entries.FirstOrDefault(e =>
            NormalizeZipPath(e.FullName).EndsWith(GlobalIniSuffix, StringComparison.OrdinalIgnoreCase));

        if (globalIniEntry is null)
        {
            throw new FileNotFoundException(
                "Could not find data/Localization/english/global.ini inside the downloaded zip. " +
                "The StarStrings release layout may have changed.");
        }

        var targetGlobalIniPath = Path.Combine(
            liveFolderPath, "data", "Localization", "english", "global.ini");

        var backedUp = BackupExistingGlobalIni(targetGlobalIniPath);
        ExtractEntryTo(globalIniEntry, targetGlobalIniPath);

        var userCfgWritten = TryInstallUserCfgIfMissing(archive, liveFolderPath);

        return new InstallResult(targetGlobalIniPath, userCfgWritten, backedUp);
    }

    private static bool TryInstallUserCfgIfMissing(ZipArchive archive, string liveFolderPath)
    {
        var existingUserCfg = Directory.EnumerateFiles(liveFolderPath)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), "user.cfg", StringComparison.OrdinalIgnoreCase));

        if (existingUserCfg is not null)
        {
            // Never overwrite a user's existing user.cfg: it may hold unrelated settings,
            // and the upstream README explicitly says not to replace it.
            return false;
        }

        var userCfgEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(NormalizeZipPath(e.FullName)), "user.cfg", StringComparison.OrdinalIgnoreCase)
            && !NormalizeZipPath(e.FullName).Contains('/'));

        if (userCfgEntry is null)
        {
            return false;
        }

        var targetUserCfgPath = Path.Combine(liveFolderPath, "user.cfg");
        ExtractEntryTo(userCfgEntry, targetUserCfgPath);
        return true;
    }

    private static bool BackupExistingGlobalIni(string targetGlobalIniPath)
    {
        if (!File.Exists(targetGlobalIniPath))
        {
            return false;
        }

        var backupPath = targetGlobalIniPath + ".bak";
        File.Copy(targetGlobalIniPath, backupPath, overwrite: true);
        return true;
    }

    private static void ExtractEntryTo(ZipArchiveEntry entry, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        var tempPath = destinationPath + ".tmp";
        entry.ExtractToFile(tempPath, overwrite: true);
        File.Move(tempPath, destinationPath, overwrite: true);
    }

    private static string NormalizeZipPath(string path) => path.Replace('\\', '/');
}
