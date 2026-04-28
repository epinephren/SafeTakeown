using System.IO;

namespace SafeTakeown.Services;

public sealed class FileLockService
{
    public bool IsFileLocked(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch
        {
            return true;
        }
    }
}