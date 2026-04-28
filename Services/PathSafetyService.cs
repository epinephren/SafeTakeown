using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System;
using System.IO;
using SafeTakeown.Models;

namespace SafeTakeown.Services;

public sealed class PathSafetyService
{
    private static readonly string[] HardBlockedPaths =
    {
        @"C:\",
        @"C:\Windows",
        @"C:\Windows\System32",
        @"C:\Windows\WinSxS",
        @"C:\Windows\Installer",
        @"C:\System Volume Information",

    };

    private static readonly string[] ExpertOnlyPaths =
    {
        @"C:\Program Files",
        @"C:\Program Files (x86)"
    };

    public bool Exists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }

    public bool IsDirectory(string path)
    {
        return Directory.Exists(path);
    }

    public PathRiskLevel GetRiskLevel(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathRiskLevel.HardBlocked;

        var full = Normalize(path);

        if (IsInList(full, HardBlockedPaths))
            return PathRiskLevel.HardBlocked;

        if (IsInList(full, ExpertOnlyPaths))
            return PathRiskLevel.ExpertOnly;

        return PathRiskLevel.Allowed;
    }

    public bool IsAllowed(string path, bool expertModeEnabled)
    {
        var risk = GetRiskLevel(path);

        return risk switch
        {
            PathRiskLevel.Allowed => true,
            PathRiskLevel.ExpertOnly => expertModeEnabled,
            PathRiskLevel.HardBlocked => false,
            _ => false
        };
    }

    private static bool IsInList(string fullPath, string[] list)
    {
        foreach (var entry in list)
        {
            var entryFull = Normalize(entry);

            // exact match
            if (string.Equals(fullPath, entryFull, StringComparison.OrdinalIgnoreCase))
                return true;

            // special case: drive root (C:\) should NOT block everything under it
            if (IsDriveRoot(entryFull))
                continue;

            // block subpaths
            if (fullPath.StartsWith(entryFull + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsDriveRoot(string path)
    {
        var root = Path.GetPathRoot(path)?.TrimEnd('\\');
        return string.Equals(path, root, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd('\\');
    }
}