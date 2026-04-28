using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SafeTakeown.Services;

public sealed class CommandRunner
{
    public async Task<string> RunAsync(string fileName, string arguments)
    {
        var output = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        output.AppendLine($"Exit Code: {process.ExitCode}");
        return output.ToString();
    }
}