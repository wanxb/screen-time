namespace ScreenTime.Services;

public static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static void Log(Exception exception, string context)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ScreenTime",
                "logs");
            Directory.CreateDirectory(directory);

            var file = Path.Combine(directory, "app.log");
            var entry = $"""
                [{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {context}
                {exception}

                """;

            lock (SyncRoot)
            {
                File.AppendAllText(file, entry);
            }
        }
        catch
        {
            // Logging must never become the reason the app exits.
        }
    }
}
