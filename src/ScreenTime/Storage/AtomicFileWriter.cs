namespace ScreenTime.Storage;

public static class AtomicFileWriter
{
    public static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    ReplaceFile(tempPath, path);
                    return;
                }
                catch (IOException) when (attempt < 3)
                {
                    await Task.Delay(75 * attempt, cancellationToken);
                }
                catch (UnauthorizedAccessException) when (attempt < 3)
                {
                    await Task.Delay(75 * attempt, cancellationToken);
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // A stale temp file is less harmful than crashing during cleanup.
            }
        }
    }

    private static void ReplaceFile(string tempPath, string path)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Replace(tempPath, path, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                File.Move(tempPath, path, overwrite: true);
            }
            return;
        }

        File.Move(tempPath, path);
    }
}
