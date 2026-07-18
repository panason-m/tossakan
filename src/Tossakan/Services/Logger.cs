namespace Tossakan.Services;

public static class Logger
{
    private static readonly object Gate = new();

    public static string LogPath { get; } = Path.Combine(AppPaths.DataFolder, "app.log");

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
    }
}
