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

        var tempPath = $"{path}.tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);

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
