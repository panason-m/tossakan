namespace Tossakan.Services;

public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tossakan");

    public static string DatabasePath { get; } = Path.Combine(DataFolder, "workmanagement.db");

    public static string AttachmentsFolder { get; } = Path.Combine(DataFolder, "attachments");

    public static string BackgroundsFolder { get; } = Path.Combine(DataFolder, "backgrounds");

    public static void EnsureFolders()
    {
        MigrateLegacyDataFolder();
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(AttachmentsFolder);
        Directory.CreateDirectory(BackgroundsFolder);
    }

    /// <summary>
    /// The app used to be called WorkManagement, storing data under %LOCALAPPDATA%\WorkManagement.
    /// Move that folder to the new Tossakan location once, so renaming the app doesn't orphan
    /// anyone's existing boards.
    /// </summary>
    private static void MigrateLegacyDataFolder()
    {
        var legacyFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkManagement");
        if (Directory.Exists(legacyFolder) && !Directory.Exists(DataFolder))
            Directory.Move(legacyFolder, DataFolder);
    }
}
