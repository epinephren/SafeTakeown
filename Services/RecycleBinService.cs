using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SafeTakeown.Services;

public sealed class RecycleBinService
{
    private readonly CommandRunner _runner = new();

    public string GetCommand(string drive)
    {
        return
            $@"takeown /f ""{drive}\$Recycle.Bin"" /r /d y" + Environment.NewLine +
            $@"icacls ""{drive}\$Recycle.Bin"" /grant Administrators:F /t /c" + Environment.NewLine +
            $@"rd /s /q ""{drive}\$Recycle.Bin""";
    }

    public async Task<string> RepairAsync(string drive)
    {
        var recycleBinPath = $@"{drive}\$Recycle.Bin";
        var log = new System.Text.StringBuilder();

        log.AppendLine("Taking ownership of Recycle Bin...");
        log.AppendLine(await _runner.RunAsync(
            "takeown",
            $@"/f ""{recycleBinPath}"" /r /d y"));

        log.AppendLine("Granting Administrators full control...");
        log.AppendLine(await _runner.RunAsync(
            "icacls",
            $@"""{recycleBinPath}"" /grant Administrators:F /t /c"));

        log.AppendLine("Deleting Recycle Bin folder...");
        log.AppendLine(await _runner.RunAsync(
            "cmd.exe",
            $@"/c rd /s /q ""{recycleBinPath}"""));

        return log.ToString();
    }
}