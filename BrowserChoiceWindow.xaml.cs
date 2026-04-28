using System.Windows;

namespace SafeTakeown;

public partial class BrowseChoiceWindow : Window
{
    public BrowseChoiceResult Choice { get; private set; } = BrowseChoiceResult.Cancel;

    public BrowseChoiceWindow()
    {
        InitializeComponent();
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        Choice = BrowseChoiceResult.Folder;
        DialogResult = true;
        Close();
    }

    private void File_Click(object sender, RoutedEventArgs e)
    {
        Choice = BrowseChoiceResult.File;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = BrowseChoiceResult.Cancel;
        DialogResult = false;
        Close();
    }
}

public enum BrowseChoiceResult
{
    Cancel,
    Folder,
    File
}