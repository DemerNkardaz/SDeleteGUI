using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SDeleteGUI.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SDeleteGUI;

public sealed partial class MainPage : Page
{
    private readonly SDeleteOptions _options = new();
    private readonly List<PathEntry> _pathEntries = new();
    private readonly ObservableCollection<DriveItem> _drives = new();

	private bool _isReady;

    public MainPage()
    {
        InitializeComponent();

		_isReady = true;

        DrivesGridView.ItemsSource = _drives;

        LoadDrives();
        RefreshCommandPreview();
    }

    // Drives management

    private void RefreshDrivesButton_Click(object sender, RoutedEventArgs e)
    {
        LoadDrives();
        RefreshCommandPreview();
    }

    private void LoadDrives()
    {
        _drives.Clear();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
                continue;

            var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Локальный диск" : drive.VolumeLabel;
            var letter = drive.Name.TrimEnd('\\');

            _drives.Add(new DriveItem
            {
                Name = drive.Name,
                DriveLetter = letter,
                DisplayName = TruncateLabel(label),
                Glyph = GetGlyphForDriveType(drive.DriveType),
                TotalSize = drive.TotalSize,
                FreeSpace = drive.TotalFreeSpace
            });
        }
    }

    private static string GetGlyphForDriveType(DriveType type) => type switch
    {
        DriveType.Removable => "\uE88E",
        DriveType.Network => "\uE8CE",
        DriveType.CDRom => "\uE958",
        DriveType.Ram => "\uEDA2",
        _ => "\uEDA2",
    };

    private void OnDrivesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady) return;
        RefreshCommandPreview();
    }

    // Paths management

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var files = await picker.PickMultipleFilesAsync();
        foreach (var file in files)
        {
            AddPathRow(file.Path, isDirectory: false);
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            AddPathRow(folder.Path, isDirectory: true);
        }
    }

    private void AddPathRow(string path, bool isDirectory, bool contentsOnly = false)
    {
        var entry = new PathEntry { Path = path, IsDirectory = isDirectory, ContentsOnly = contentsOnly };
        _pathEntries.Add(entry);

        var row = BuildPathRow(entry);
        PathsPanel.Children.Add(row);

        UpdateEmptyPathsHint();
        RefreshCommandPreview();
    }

    private FrameworkElement BuildPathRow(PathEntry entry)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pathTextBox = new TextBox
        {
            Text = entry.Path,
            IsReadOnly = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(pathTextBox, 0);

        var contentsOnlyCheckBox = new CheckBox
        {
            Content = "Только содержимое",
            IsChecked = entry.ContentsOnly,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = entry.IsDirectory ? Visibility.Visible : Visibility.Collapsed
        };
        contentsOnlyCheckBox.Checked += (_, _) => { entry.ContentsOnly = true; RefreshCommandPreview(); };
        contentsOnlyCheckBox.Unchecked += (_, _) => { entry.ContentsOnly = false; RefreshCommandPreview(); };
        Grid.SetColumn(contentsOnlyCheckBox, 1);

        var changeButton = new Button { Content = "Изменить" };
        changeButton.Click += async (_, _) => await ChangePathAsync(entry, pathTextBox, contentsOnlyCheckBox);
        Grid.SetColumn(changeButton, 2);

        var removeButton = new Button { Content = "✕" };
        removeButton.Click += (_, _) => RemovePathRow(entry, grid);
        Grid.SetColumn(removeButton, 3);

        grid.Children.Add(pathTextBox);
        grid.Children.Add(contentsOnlyCheckBox);
        grid.Children.Add(changeButton);
        grid.Children.Add(removeButton);

        return grid;
    }

    private async Task ChangePathAsync(PathEntry entry, TextBox pathTextBox, CheckBox contentsOnlyCheckBox)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            entry.Path = file.Path;
            entry.IsDirectory = false;
            entry.ContentsOnly = false;
            pathTextBox.Text = file.Path;
            contentsOnlyCheckBox.IsChecked = false;
            contentsOnlyCheckBox.Visibility = Visibility.Collapsed;
            RefreshCommandPreview();
        }
    }

    private void RemovePathRow(PathEntry entry, FrameworkElement row)
    {
        _pathEntries.Remove(entry);
        PathsPanel.Children.Remove(row);
        UpdateEmptyPathsHint();
        RefreshCommandPreview();
    }

    private void UpdateEmptyPathsHint()
    {
        EmptyPathsHint.Visibility = _pathEntries.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private nint GetWindowHandle() => WindowNative.GetWindowHandle(App.MainWindow);

    // Common options

    private async void BrowseSdeleteFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            SdeleteFolderTextBox.Text = folder.Path;
        }
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_isReady) return;
        RefreshCommandPreview();
    }


    private void OnPassesChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_isReady) return;
        RefreshCommandPreview();
    }

    private void OnSdeleteFolderChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isReady) return;
        RefreshCommandPreview();
    }

    private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady) return;
        RefreshCommandPreview();
    }


    // Synchronization and command preview

    private void SyncOptionsFromUI()
    {
        _options.Mode = MainPivot.SelectedIndex == 0 ? SDeleteMode.DeleteFiles : SDeleteMode.CleanFreeSpace;

        _options.Executable = Sdelete64Radio.IsChecked == true ? SDeleteExecutable.Sdelete64 : SDeleteExecutable.Sdelete;
        _options.SdeleteFolderPath = string.IsNullOrWhiteSpace(SdeleteFolderTextBox.Text) ? null : SdeleteFolderTextBox.Text;
        _options.Passes = double.IsNaN(PassesNumberBox.Value) ? 1 : (int)PassesNumberBox.Value;

        _options.TargetPaths = _pathEntries.ToList();
        _options.Recursive = RecurseCheckBox.IsChecked == true;
        _options.RemoveReadOnlyAttribute = RemoveReadOnlyCheckBox.IsChecked == true;

        _options.SelectedDrives = DrivesGridView.SelectedItems
            .Cast<DriveItem>()
            .Select(d => d.Name)
            .ToList();
        _options.CleanMode = ZeroFillRadio.IsChecked == true ? CleanMode.ZeroFill : CleanMode.CleanFree;
    }

	private void RefreshCommandPreview()
	{
		try
		{
			SyncOptionsFromUI();
			var errors = _options.Validate();

			OutputTextBox.Text = errors.Count > 0
				? "Команда не может быть сформирована:\n" + string.Join('\n', errors.Select(err => "- " + err))
				: "$ " + _options.BuildCommandLine();
		}
		catch (Exception ex)
		{
			OutputTextBox.Text = $"[Внутренняя ошибка при построении команды]\n{ex}";
		}
	}

    private void RunButton_Click(object sender, RoutedEventArgs e) { }

    private static string TruncateLabel(string label, int maxLength = 9)
    {
        return label.Length > maxLength
            ? label[..maxLength] + "…"
            : label;
    }
}
