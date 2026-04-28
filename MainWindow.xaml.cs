using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using Microsoft.Win32;
using SafeTakeown.Models;
using SafeTakeown.Services;


namespace SafeTakeown;

public partial class MainWindow : Window
{
    private readonly PathSafetyService _pathSafety = new();
    private readonly CommandRunner _commandRunner = new();
    private readonly DeleteService _deleteService = new();
    private readonly RecycleBinService _recycleBin = new();
    private readonly FileLockService _fileLock = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadDrives();
        Log("SafeTakeown started.");
    }
    
    private void Help_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "SafeTakeown \n\n" +
            "Created by Epi Nephren\n" +
            "helps advanced Windows users repair permissions, take ownership, and clean up stubborn files.\n\n" +
            "It does not bypass Windows security. It only automates visible administrative actions.\n\n" +
             "Open project page?",
            "SafeTakeown Help",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.Yes)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "https://epinephren.github.io",
            UseShellExecute = true
        });
    }
    
    private void Log(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }

    private string CurrentPath => PathTextBox.Text.Trim();

    private bool ValidatePath()
    {
        var path = CurrentPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            Log("No path selected.");
            return false;
        }

        if (!_pathSafety.Exists(path))
        {
            Log($"Path does not exist: {path}");
            return false;
        }

        var risk = _pathSafety.GetRiskLevel(path);
        var expert = ExpertModeCheckBox.IsChecked == true;
        var allowed = _pathSafety.IsAllowed(path, expert);

        Log($"Path: {path}");
        Log($"Type: {(_pathSafety.IsDirectory(path) ? "Folder" : "File")}");
        Log($"Risk: {risk}");
        Log($"Expert Mode: {expert}");

        if (!_pathSafety.IsDirectory(path))
        {
            var locked = _fileLock.IsFileLocked(path);
            Log($"Locked: {locked}");
        }

        if (!allowed)
        {
            Log("BLOCKED: Operation not allowed.");
            return false;
        }

        Log("Allowed.");
        return true;
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        ValidatePath();
    }

    private void DryRun_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidatePath())
            return;

        var path = CurrentPath;
        var isDir = _pathSafety.IsDirectory(path);

        Log("Dry run preview:");

        if (isDir)
        {
            Log($@"takeown /f ""{path}"" /r /d y");
            Log($@"icacls ""{path}"" /grant Administrators:F /t");
        }
        else
        {
            Log($@"takeown /f ""{path}""");
            Log($@"icacls ""{path}"" /grant Administrators:F");
        }
    }

    private async void TakeOwnership_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidatePath())
            return;

        var confirm = MessageBox.Show(
            "This will take ownership and grant Administrators full control. Continue?",
            "Confirm operation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        var path = CurrentPath;
        var isDir = _pathSafety.IsDirectory(path);

        Log("Running takeown...");

        var takeownArgs = isDir
            ? $@"/f ""{path}"" /r /d y"
            : $@"/f ""{path}""";

        Log(await _commandRunner.RunAsync("takeown", takeownArgs));

        Log("Running icacls...");

        var icaclsArgs = isDir
            ? $@"""{path}"" /grant Administrators:F /t"
            : $@"""{path}"" /grant Administrators:F";

        Log(await _commandRunner.RunAsync("icacls", icaclsArgs));

        Log("Permission operation finished.");
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidatePath())
            return;

        var path = CurrentPath;
        UpdateStatus(path);
        var isDir = _pathSafety.IsDirectory(path);

        var selectedMode = DeleteModeComboBox.SelectedIndex;
        var secureDelete = selectedMode == 1;

        if (secureDelete && isDir)
        {
            Log("Secure Delete is only available for files, not folders.");
            return;
        }

        Log("Delete dry run:");

        if (secureDelete)
        {
            Log($"Secure Delete 1-pass overwrite: {path}");
            Log($"Then delete file: {path}");
        }
        else
        {
            Log(_deleteService.GetDeleteCommand(path, isDir));
        }

        var warning = secureDelete
            ? "This will overwrite the selected file once and then permanently delete it. Not guaranteed on SSDs. Continue?"
            : "This will permanently delete the selected path. Continue?";

        var confirm = MessageBox.Show(
            warning,
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            if (secureDelete)
            {
                _deleteService.SecureDeleteSinglePass(path);
                Log($"Secure deleted: {path}");
            }
            else
            {
                _deleteService.DeleteNow(path, isDir);
                Log($"Deleted: {path}");
            }
        }
        catch (Exception ex)
        {
            Log($"Delete failed: {ex.Message}");
            Log("Try Delete on Reboot if the file is locked.");
        }
    }

    private void DeleteOnReboot_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidatePath())
            return;

        var path = CurrentPath;

        Log("Delete on reboot dry run:");
        Log($"MoveFileEx DELETE_ON_REBOOT: {path}");

        var confirm = MessageBox.Show(
            "This will schedule the selected path for deletion on next reboot. Continue?",
            "Confirm Delete on Reboot",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var success = _deleteService.ScheduleDeleteOnReboot(path);

            if (success)
                Log($"Scheduled for deletion on reboot: {path}");
            else
                Log("Failed to schedule deletion on reboot.");
        }
        catch (Exception ex)
        {
            Log($"Delete on reboot failed: {ex.Message}");
        }
    }

    private async void RepairRecycleBin_Click(object sender, RoutedEventArgs e)
    {
        if (RecycleBinDriveComboBox.SelectedItem is not string drive)
        {
            Log("No drive selected for Recycle Bin repair.");
            return;
        }

        Log($"Recycle Bin repair selected for {drive}");

        var command = _recycleBin.GetCommand(drive);

        Log("Dry run:");
        Log(command);

        var confirm = MessageBox.Show(
            $"This will reset the Recycle Bin on {drive}. Continue?",
            "Confirm Recycle Bin Repair",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        Log("Running recycle bin repair...");

        var result = await _recycleBin.RepairAsync(drive);

        Log(result);
        Log("Recycle Bin repair finished.");
    }

    private void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "SafeTakeown-log.txt",
            Filter = "Text files (*.txt)|*.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, LogTextBox.Text);
            Log($"Log exported: {dialog.FileName}");
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length > 0)
            {
                PathTextBox.Text = files[0];
                Log($"Path dropped: {files[0]}");
                ValidatePath();
                UpdateStatus(PathTextBox.Text);
            }
        }
    }

    private void LoadDrives()
    {
        RecycleBinDriveComboBox.Items.Clear();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType == DriveType.Fixed && drive.IsReady)
            {
                RecycleBinDriveComboBox.Items.Add(drive.Name.TrimEnd('\\'));
            }
        }

        if (RecycleBinDriveComboBox.Items.Count > 0)
            RecycleBinDriveComboBox.SelectedIndex = 0;
    }
    private void PathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
{
    var path = CurrentPath;

    UpdateStatus(path);

    if (!string.IsNullOrWhiteSpace(path))
    {
        if (_pathSafety.Exists(path))
        {
            Log($"Path changed: {path}");
        }
    }
}
    private void ExpertModeChanged(object sender, RoutedEventArgs e)
    {
        UpdateStatus(CurrentPath);
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var choiceWindow = new BrowseChoiceWindow
        {
            Owner = this
        };

        var result = choiceWindow.ShowDialog();

        if (result != true || choiceWindow.Choice == BrowseChoiceResult.Cancel)
            return;

        if (choiceWindow.Choice == BrowseChoiceResult.Folder)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select a folder"
            };

            if (folderDialog.ShowDialog() == true)
            {
                PathTextBox.Text = folderDialog.FolderName;
                Log($"Path selected: {folderDialog.FolderName}");
                ValidatePath();
                UpdateStatus(PathTextBox.Text);
            }
        }
        else if (choiceWindow.Choice == BrowseChoiceResult.File)
        {
            var fileDialog = new OpenFileDialog
            {
                Title = "Select a file",
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (fileDialog.ShowDialog() == true)
            {
                PathTextBox.Text = fileDialog.FileName;
                Log($"Path selected: {fileDialog.FileName}");
                ValidatePath();
            }
        }
    }
    private void UpdateStatus(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusTextBlock.Text = "";
            return;
        }

        var risk = _pathSafety.GetRiskLevel(path);
        var expert = ExpertModeCheckBox.IsChecked == true;

        switch (risk)
        {
            case PathRiskLevel.Allowed:
                StatusTextBlock.Text = "🟢 Allowed";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                break;

            case PathRiskLevel.ExpertOnly:
                if (expert)
                {
                    StatusTextBlock.Text = "🟡 Expert Mode (Allowed)";
                    StatusTextBlock.Foreground = System.Windows.Media.Brushes.Goldenrod;
                }
                else
                {
                    StatusTextBlock.Text = "🟡 Expert Only (Blocked)";
                    StatusTextBlock.Foreground = System.Windows.Media.Brushes.DarkOrange;
                }
                break;

            case PathRiskLevel.HardBlocked:
                StatusTextBlock.Text = "🔴 Blocked (System Protected)";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                break;
        }
    }
}