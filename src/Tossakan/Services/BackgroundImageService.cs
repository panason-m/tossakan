namespace Tossakan.Services;

public class BackgroundImageService
{
    /// <summary>
    /// Stored in Board.BackgroundImagePath to mean "use the bundled default photo". Resolving it to an
    /// actual path is deferred to render time (via <see cref="DefaultImagePath"/>) rather than saved as
    /// one, because the absolute path to the bundled asset depends on which build (Debug/Release/publish)
    /// is currently running and would go stale the moment that changes.
    /// </summary>
    public const string DefaultImageMarker = "__default__";

    /// <summary>The bundled default board background photo, copied next to the exe as Content.</summary>
    public static string DefaultImagePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Backgrounds", "night.jpg");

    /// <summary>Same convention as <see cref="DefaultImageMarker"/>, but for the home page's bundled photo.</summary>
    public const string HomeDefaultImageMarker = "__home_default__";

    /// <summary>The bundled default home page background photo, copied next to the exe as Content.</summary>
    public static string DefaultHomeImagePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Backgrounds", "home.jpg");

    /// <summary>Copies a user-picked image into the app's backgrounds folder and returns the stored path.</summary>
    public async Task<string> ImportCustomImageAsync(string sourceFilePath)
    {
        AppPaths.EnsureFolders();
        var fileName = Path.GetFileName(sourceFilePath);
        var storedPath = Path.Combine(AppPaths.BackgroundsFolder, $"{Guid.NewGuid():N}_{fileName}");
        await Task.Run(() => File.Copy(sourceFilePath, storedPath));
        return storedPath;
    }

    /// <summary>True if the path was imported into the app's own backgrounds folder (safe to delete when unused).</summary>
    public static bool IsOwnedCustomImage(string path) =>
        path.StartsWith(AppPaths.BackgroundsFolder, StringComparison.OrdinalIgnoreCase);
}
