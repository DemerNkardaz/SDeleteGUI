using System.Diagnostics;
using Microsoft.UI.Dispatching;
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

    private static string TruncateLabel(string label, int maxLength = 9)
    {
        return label.Length > maxLength
            ? label[..maxLength] + "…"
            : label;
    }

	private async void RunButton_Click(object sender, RoutedEventArgs e)
	{
		SyncOptionsFromUI();
		var errors = _options.Validate();
		if (errors.Count > 0)
		{
			RefreshCommandPreview();
			return;
		}

		var confirmed = await ShowConfirmationDialogAsync();
		if (!confirmed)
			return;

		SetUiEnabled(false);
		try
		{
			await RunSDeleteAsync();
		}
		finally
		{
			SetUiEnabled(true);
		}
	}

	/// <summary>
	/// Shows a confirmation dialog to the user before running sdelete, with a countdown timer on the "Yes" button.
	/// The user must wait for the countdown to finish before they can confirm the action.
	/// This is to prevent accidental execution of the destructive command.
	/// </summary>
	private async Task<bool> ShowConfirmationDialogAsync()
	{
		ConfirmDialogText.Text = _options.Mode == SDeleteMode.DeleteFiles
			? "Запуск данной команды необратимо уничтожит выбранные файлы и папки. Даже используя инструменты восстановления удалённых файлов вы не сможете их восстановить.\n\nХотите продолжить?"
			: "Запуск данной команды запустит процесс очистки свободного места на выбранных накопителях. В зависимости от объёма накопителя и размера свободного места, процесс может занять длительное время, вплоть до 10 часов и более.\n\nВ течение этого времени не используйте выбранные накопители. Безопасное удаление временно заполнит всё свободное место на них.\n\nХотите продолжить?";

		const int countdownSeconds = 5;
		var remaining = countdownSeconds;

		ConfirmDialog.IsPrimaryButtonEnabled = false;
		ConfirmDialog.PrimaryButtonText = $"Да ({remaining})";

		var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		timer.Tick += (_, _) =>
		{
			remaining--;
			if (remaining <= 0)
			{
				timer.Stop();
				ConfirmDialog.PrimaryButtonText = "Да";
				ConfirmDialog.IsPrimaryButtonEnabled = true;
			}
			else
			{
				ConfirmDialog.PrimaryButtonText = $"Да ({remaining})";
			}
		};
		timer.Start();

		ConfirmDialog.XamlRoot = this.XamlRoot;
		var result = await ConfirmDialog.ShowAsync();

		timer.Stop();

		return result == ContentDialogResult.Primary;
	}

	/// <summary>
	/// Blocks or unblocks the main UI elements (pivot, common options panel, run button)
	/// based on the provided boolean value.
	/// </summary>
	private void SetUiEnabled(bool enabled)
	{
		MainPivot.IsEnabled = enabled;
		CommonOptionsPanel.IsEnabled = enabled;
		RunButton.IsEnabled = enabled;
	}

	/// <summary>
	/// Starts the sdelete process with the current options,
	/// captures its output and error streams, and appends them to the OutputTextBox in real-time.
	/// </summary>
	private async Task RunSDeleteAsync()
	{
		var dispatcher = DispatcherQueue.GetForCurrentThread();

		var psi = new ProcessStartInfo
		{
			FileName = _options.ResolveExecutablePath(),
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};
		foreach (var arg in _options.BuildArguments())
		{
			psi.ArgumentList.Add(arg);
		}

		// App Execution Alias stub-файлы в этой папке ломают резолвинг через
		// голый CreateProcess (в отличие от PowerShell/cmd), поэтому исключаем
		// её из PATH, который видит только этот конкретный дочерний процесс —
		// сама команда в превью при этом остаётся с голым именем exe.
		var windowsAppsAlias = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Microsoft", "WindowsApps");

		var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
		var filteredPath = string.Join(
			Path.PathSeparator,
			currentPath.Split(Path.PathSeparator)
				.Where(p => !string.Equals(p.Trim('"'), windowsAppsAlias, StringComparison.OrdinalIgnoreCase)));

		psi.EnvironmentVariables["PATH"] = filteredPath;

		AppendOutputLine("");
		AppendOutputLine("--- Запуск ---");

		try
		{
			using var process = new Process { StartInfo = psi };

			process.OutputDataReceived += (_, e) =>
			{
				if (e.Data != null)
					dispatcher.TryEnqueue(() => AppendOutputLine(e.Data));
			};
			process.ErrorDataReceived += (_, e) =>
			{
				if (e.Data != null)
					dispatcher.TryEnqueue(() => AppendOutputLine(e.Data));
			};

			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			await process.WaitForExitAsync();

			AppendOutputLine($"--- Завершено с кодом {process.ExitCode} ---");
		}
		catch (Exception ex)
		{
			AppendOutputLine($"--- Ошибка запуска: {ex.Message} ---");
		}
	}

	private void AppendOutputLine(string line)
	{
		OutputTextBox.Text += Environment.NewLine + line;
	}
}
