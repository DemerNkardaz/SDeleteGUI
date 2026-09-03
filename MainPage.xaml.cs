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

    public MainPage()
    {
        InitializeComponent();
        DrivesGridView.ItemsSource = _drives;
        LoadDrives();
    }

    // Drives

    private void RefreshDrivesButton_Click(object sender, RoutedEventArgs e)
    {
        LoadDrives();
    }

    /// <summary>
    /// Lists all available drives and populates
	/// the _drives collection with DriveItem instances
	/// for each ready drive. Skips network drives
	/// and empty drives without media.
    /// </summary>
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
        DriveType.Removable => "\uE88E", // USB/SD card
        DriveType.Network => "\uE8CE",   // net drive
        DriveType.CDRom => "\uE958",     // optical drive
        DriveType.Ram => "\uEDA2",       // RAM-drive (the same icon as fixed — can be replaced if desired)
        _ => "\uEDA2",                    // fixed (HDD/SSD)
    };

    // Paths addition/removal

	private async void AddFiles_Click(object sender, RoutedEventArgs e)
	{
		var picker = new FileOpenPicker
		{
			SuggestedStartLocation = PickerLocationId.ComputerFolder
		};
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
		var picker = new FolderPicker
		{
			SuggestedStartLocation = PickerLocationId.ComputerFolder
		};
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
	}

    /// <summary>
    /// Creates a UI row for a given PathEntry, including the path display, “contents only” checkbox, and buttons to change or remove the path.
    /// </summary>
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
			// Для файла флаг бессмысленен — файл сам по себе и есть содержимое.
			Visibility = entry.IsDirectory ? Visibility.Visible : Visibility.Collapsed
		};
		contentsOnlyCheckBox.Checked += (_, _) => entry.ContentsOnly = true;
		contentsOnlyCheckBox.Unchecked += (_, _) => entry.ContentsOnly = false;
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

    /// <summary>
    /// Shows a file picker to change the path of a given PathEntry. Updates the UI elements accordingly.
    /// </summary>
	private async Task ChangePathAsync(PathEntry entry, TextBox pathTextBox, CheckBox contentsOnlyCheckBox)
	{
		var picker = new FileOpenPicker
		{
			SuggestedStartLocation = PickerLocationId.ComputerFolder
		};
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
		}
	}

    private void RemovePathRow(PathEntry entry, FrameworkElement row)
    {
        _pathEntries.Remove(entry);
        PathsPanel.Children.Remove(row);
        UpdateEmptyPathsHint();
    }

    private void UpdateEmptyPathsHint()
    {
        EmptyPathsHint.Visibility = _pathEntries.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private nint GetWindowHandle()
    {
        return WindowNative.GetWindowHandle(App.MainWindow);
    }

    // Etcetera
    private void BrowseSdeleteFolderButton_Click(object sender, RoutedEventArgs e) { }

    private void SyncOptionsFromUI()
    {
        _options.TargetPaths = _pathEntries.ToList();
        _options.SelectedDrives = DrivesGridView.SelectedItems
            .Cast<DriveItem>()
            .Select(d => d.Name)
            .ToList();
    }

    private void RunButton_Click(object sender, RoutedEventArgs e) { }



	private static string TruncateLabel(string label, int maxLength = 9)
	{
		return label.Length > maxLength
			? label[..maxLength] + "…"
			: label;
	}
}
