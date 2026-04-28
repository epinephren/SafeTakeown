using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SafeTakeown.Services;

public sealed class DeleteService
{
    private const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, int dwFlags);

    public string GetDeleteCommand(string path, bool isDirectory)
    {
        return isDirectory
            ? $@"rd /s /q ""{path}"""
            : $@"del /f /q ""{path}""";
    }
    
    
    public void SecureDeleteSinglePass(string path)
    {
        if (!File.Exists(path))
            return;

        var fileInfo = new FileInfo(path);
        var length = fileInfo.Length;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        var buffer = new byte[8192];
        var rng = new Random();

        long written = 0;

        while (written < length)
        {
            rng.NextBytes(buffer);

            var toWrite = (int)Math.Min(buffer.Length, length - written);
            stream.Write(buffer, 0, toWrite);
            written += toWrite;
        }

        stream.Flush(true);
        stream.Close();

        File.Delete(path);
    }
    
    
    public void DeleteNow(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    public bool ScheduleDeleteOnReboot(string path)
    {
        return MoveFileEx(path, null, MOVEFILE_DELAY_UNTIL_REBOOT);
    }
}