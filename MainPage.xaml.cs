using System.Diagnostics;
using Microsoft.UI.Dispatching;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;
using System.Runtime.InteropServices;

using SDeleteGUI.Models;
using SDeleteGUI.Services;

namespace SDeleteGUI;

public sealed partial class MainPage : Page
{
	private readonly SDeleteOptions _options = new();
	private readonly List<PathEntry> _pathEntries = new();
	private readonly ObservableCollection<DriveItem> _drives = new();

	private bool _isReady;

	private bool _isRunning;

	private DispatcherTimer? _statusTimer;
	private DateTime _processStartTime;

	private Brush? _originalButtonPointerOverBackground;
	private Brush? _originalButtonPressedBackground;

	private readonly StringBuilder _outputBuffer = new();

	private ScrollViewer? _outputScrollViewer;

	public bool IsRunning => _isRunning;

	public MainPage()
	{
		InitializeComponent();
		InitializeStatusBar();

		RunButton.Content = Loc.Get("RunButton");

		DrivesGridView.ItemsSource = _drives;

		LoadDrives();

		var settings = SettingsService.Load();
		ApplySettingsToUI(settings);
		UpdateSdeletePresenceWarning();

		ApplyStartupArguments(App.StartupArguments);

		_isReady = true;

		RefreshCommandPreview();

	}

	public async Task<bool> ConfirmCloseWhileRunningAsync()
	{
		var dialog = new ContentDialog
		{
			XamlRoot = this.XamlRoot,
			Title = Loc.Get("CloseWhileRunningTitle"),
			Content = Loc.Get("CloseWhileRunningText"),
			PrimaryButtonText = Loc.Get("CloseWhileRunningYes"),
			CloseButtonText = Loc.Get("CloseWhileRunningNo"),
			DefaultButton = ContentDialogButton.Close
		};

		var result = await dialog.ShowAsync();
		return result == ContentDialogResult.Primary;
	}

	public void RequestStopForClose()
	{
		PseudoConsoleProcessRunner.RequestStop();
	}

	private void InitializeStatusBar()
	{
		var informationalVersion = Assembly.GetExecutingAssembly()
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion;

		var version = informationalVersion?.Split('+')[0] ?? "1.0.0";

		StatusVersionText.Text = version;
		StatusAuthorLink.Content = Loc.Get("AuthorName");

		SetStatusIdle();
	}

	private void SetStatusIdle()
	{
		StatusIcon.Glyph = "\uE945"; // нейтральная точка
		StatusIcon.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
		StatusIcon.Visibility = Visibility.Visible;
		StatusProgressRing.IsActive = false;
		StatusProgressRing.Visibility = Visibility.Collapsed;
	}

	private void SetStatusRunning()
	{
		StatusIcon.Visibility = Visibility.Collapsed;
		StatusProgressRing.IsActive = true;
		StatusProgressRing.Visibility = Visibility.Visible;
		_processStartTime = DateTime.Now;
		StatusTimerText.Text = "00:00:00";
		StartStatusTimer();
	}

	private void SetStatusCompleted(int exitCode)
	{
		StopStatusTimer();
		StatusProgressRing.IsActive = false;
		StatusProgressRing.Visibility = Visibility.Collapsed;
		StatusIcon.Visibility = Visibility.Visible;

		if (exitCode == 0)
		{
			StatusIcon.Glyph = "\uE73E"; // галочка
			StatusIcon.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
		}
		else
		{
			StatusIcon.Glyph = "\uEA39"; // ошибка
			StatusIcon.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
		}
	}

	private void SetStatusStopped()
	{
		StopStatusTimer();
		StatusProgressRing.IsActive = false;
		StatusProgressRing.Visibility = Visibility.Collapsed;
		StatusIcon.Visibility = Visibility.Visible;
		StatusIcon.Glyph = "\uE711"; // отмена
		StatusIcon.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
	}

	private void StartStatusTimer()
	{
		_statusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_statusTimer.Tick -= OnStatusTimerTick;
		_statusTimer.Tick += OnStatusTimerTick;
		_statusTimer.Start();
	}

	private void StopStatusTimer()
	{
		_statusTimer?.Stop();
	}

	private void OnStatusTimerTick(object? sender, object e)
	{
		var elapsed = DateTime.Now - _processStartTime;
		StatusTimerText.Text = elapsed.ToString(@"hh\:mm\:ss");
	}

	private static readonly System.Text.RegularExpressions.Regex DriveRootRegex =
		new(@"^[A-Za-z]:\\?$", System.Text.RegularExpressions.RegexOptions.Compiled);

	private void ApplyStartupArguments(string[] args)
	{
		if (args.Length == 0) return;

		var driveArgs = new List<string>();
		var pathArgs = new List<string>();

		foreach (var arg in args)
		{
			var trimmed = arg.Trim();
			if (trimmed.Length == 0) continue;

			if (DriveRootRegex.IsMatch(trimmed))
				driveArgs.Add(trimmed.TrimEnd('\\').ToUpperInvariant());
			else if (Directory.Exists(trimmed) || File.Exists(trimmed))
				pathArgs.Add(trimmed);
		}

		if (pathArgs.Count > 0)
		{
			MainPivot.SelectedIndex = 0;
			foreach (var path in pathArgs)
				AddPathRow(path, isDirectory: Directory.Exists(path));
		}
		else if (driveArgs.Count > 0)
		{
			MainPivot.SelectedIndex = 1;
			foreach (var item in _drives.Where(d => driveArgs.Contains(d.DriveLetter, StringComparer.OrdinalIgnoreCase)))
				DrivesGridView.SelectedItems.Add(item);
		}
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

			var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? Loc.Get("LocalDisk") : drive.VolumeLabel;
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
		RecalculatePathStats();
	}

	private void ClearPathsButton_Click(object sender, RoutedEventArgs e)
	{
		_pathEntries.Clear();

		for (var i = PathsPanel.Children.Count - 1; i >= 0; i--)
		{
			if (!ReferenceEquals(PathsPanel.Children[i], EmptyPathsHint))
				PathsPanel.Children.RemoveAt(i);
		}

		UpdateEmptyPathsHint();
		RefreshCommandPreview();
		RecalculatePathStats();
	}

	// Drag-and-drop

	private void RootGrid_DragOver(object sender, DragEventArgs e)
	{
		if (_isRunning ||
			!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
		{
			e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
			return;
		}

		e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
		e.DragUIOverride.Caption = Loc.Get("DropToAddCaption");
	}

	private async void RootGrid_Drop(object sender, DragEventArgs e)
	{
		if (_isRunning ||
			!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
			return;

		var deferral = e.GetDeferral();
		try
		{
			var items = await e.DataView.GetStorageItemsAsync();
			var added = false;

			foreach (var item in items)
			{
				if (string.IsNullOrEmpty(item.Path))
					continue;

				if (_pathEntries.Any(p => string.Equals(p.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
					continue;

				AddPathRow(item.Path, isDirectory: item.IsOfType(Windows.Storage.StorageItemTypes.Folder));
				added = true;
			}

			if (added)
				MainPivot.SelectedIndex = 0;
		}
		finally
		{
			deferral.Complete();
		}
	}

	private FrameworkElement BuildPathRow(PathEntry entry)
	{
		var grid = new Grid { ColumnSpacing = 8 };
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		var iconFallback = new FontIcon
		{
			Glyph = entry.IsDirectory ? "\uE8B7" : "\uE8A5",
			FontSize = 20,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		var iconImage = new Image { Width = 24, Height = 24, Visibility = Visibility.Collapsed };
		var iconHost = new Grid { Width = 24, Height = 24, VerticalAlignment = VerticalAlignment.Center };
		iconHost.Children.Add(iconFallback);
		iconHost.Children.Add(iconImage);

		var iconButton = new Button
		{
			Style = (Style)Resources["PathIconButtonStyle"],
			Content = iconHost
		};
		ToolTipService.SetToolTip(iconButton, Loc.Get("OpenPathToolTip"));
		iconButton.Click += (_, _) => OpenPathInExplorer(entry);
		Grid.SetColumn(iconButton, 0);
		_ = LoadPathIconAsync(entry, iconImage, iconFallback);

		var pathTextBox = new TextBox
		{
			Text = entry.Path,
			IsReadOnly = true,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetColumn(pathTextBox, 1);

		var contentsOnlyCheckBox = new CheckBox
		{
			Content = Loc.Get("ContentsOnlyCheckBox"),
			IsChecked = entry.ContentsOnly,
			VerticalAlignment = VerticalAlignment.Center,
			Visibility = entry.IsDirectory ? Visibility.Visible : Visibility.Collapsed
		};
		contentsOnlyCheckBox.Checked += (_, _) => { entry.ContentsOnly = true; RefreshCommandPreview(); };
		contentsOnlyCheckBox.Unchecked += (_, _) => { entry.ContentsOnly = false; RefreshCommandPreview(); };
		Grid.SetColumn(contentsOnlyCheckBox, 2);

		var changeButton = new Button { Content = Loc.Get("ChangePathButton") };
		changeButton.Click += async (_, _) =>
			await ChangePathAsync(entry, pathTextBox, contentsOnlyCheckBox, iconImage, iconFallback);
		Grid.SetColumn(changeButton, 3);

		var removeButton = new Button { Content = "✕" };
		removeButton.Click += (_, _) => RemovePathRow(entry, grid);
		Grid.SetColumn(removeButton, 4);

		grid.Children.Add(iconButton);
		grid.Children.Add(pathTextBox);
		grid.Children.Add(contentsOnlyCheckBox);
		grid.Children.Add(changeButton);
		grid.Children.Add(removeButton);

		return grid;
	}

	private async Task ChangePathAsync(
		PathEntry entry,
		TextBox pathTextBox,
		CheckBox contentsOnlyCheckBox,
		Image iconImage,
		FontIcon iconFallback)
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

			iconImage.Source = null;
			iconImage.Visibility = Visibility.Collapsed;
			iconFallback.Glyph = "\uE8A5";
			iconFallback.Visibility = Visibility.Visible;
			_ = LoadPathIconAsync(entry, iconImage, iconFallback);

			RefreshCommandPreview();
			RecalculatePathStats();
		}
	}

	private static async Task LoadPathIconAsync(PathEntry entry, Image image, FontIcon fallback)
	{
		try
		{
			Windows.Storage.IStorageItemProperties item;
			if (entry.IsDirectory)
				item = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(entry.Path);
			else
				item = await Windows.Storage.StorageFile.GetFileFromPathAsync(entry.Path);

			using var thumbnail = await item.GetThumbnailAsync(
				Windows.Storage.FileProperties.ThumbnailMode.ListView,
				32,
				Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);

			if (thumbnail is null)
				return;

			var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
			await bitmap.SetSourceAsync(thumbnail);

			image.Source = bitmap;
			image.Visibility = Visibility.Visible;
			fallback.Visibility = Visibility.Collapsed;
		}
		catch
		{
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHParseDisplayName(
		string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

	[DllImport("shell32.dll")]
	private static extern int SHOpenFolderAndSelectItems(
		IntPtr pidlFolder, uint cidl, IntPtr[]? apidl, uint dwFlags);

	[DllImport("ole32.dll")]
	private static extern void CoTaskMemFree(IntPtr pv);

	private static void OpenPathInExplorer(PathEntry entry)
	{
		try
		{
			if (entry.IsDirectory)
			{
				if (Directory.Exists(entry.Path))
					Process.Start(new ProcessStartInfo(entry.Path) { UseShellExecute = true });
				return;
			}

			if (!File.Exists(entry.Path))
				return;

			if (!TrySelectInShell(entry.Path))
			{
				var parent = Path.GetDirectoryName(entry.Path);
				if (!string.IsNullOrEmpty(parent))
					Process.Start(new ProcessStartInfo(parent) { UseShellExecute = true });
			}
		}
		catch
		{
		}
	}

	private static bool TrySelectInShell(string path)
	{
		var pidl = IntPtr.Zero;
		try
		{
			if (SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _) != 0 || pidl == IntPtr.Zero)
				return false;

			return SHOpenFolderAndSelectItems(pidl, 0, null, 0) == 0;
		}
		finally
		{
			if (pidl != IntPtr.Zero)
				CoTaskMemFree(pidl);
		}
	}

	private void RemovePathRow(PathEntry entry, FrameworkElement row)
	{
		_pathEntries.Remove(entry);
		PathsPanel.Children.Remove(row);
		UpdateEmptyPathsHint();
		RefreshCommandPreview();
		RecalculatePathStats();
	}

	private void UpdateEmptyPathsHint()
	{
		EmptyPathsHint.Visibility = _pathEntries.Count == 0
			? Visibility.Visible
			: Visibility.Collapsed;
	}

	// Path statistics

	private enum PathStatsState { Counting, Done, Stopped }

	private CancellationTokenSource? _statsCts;
	private PathStats _lastStats;
	private bool _statsInProgress;

	private async void RecalculatePathStats()
	{
		_statsCts?.Cancel();

		if (_pathEntries.Count == 0)
		{
			_statsInProgress = false;
			PathsStatsText.Text = string.Empty;
			return;
		}

		var cts = new CancellationTokenSource();
		_statsCts = cts;
		_statsInProgress = true;
		_lastStats = default;

		var snapshot = _pathEntries.Select(p => (p.Path, p.IsDirectory)).ToList();

		var progress = new Progress<PathStats>(stats =>
		{
			if (cts.IsCancellationRequested)
				return;

			_lastStats = stats;
			ShowStats(stats, PathStatsState.Counting);
		});

		try
		{
			await Task.Delay(150, cts.Token);

			var result = await Task.Run(
				() => PathStatsCalculator.Calculate(snapshot, progress, cts.Token),
				cts.Token);

			if (cts.IsCancellationRequested)
				return;

			_lastStats = result;
			ShowStats(result, PathStatsState.Done);
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			if (ReferenceEquals(_statsCts, cts))
				_statsInProgress = false;
		}
	}

	private void StopPathStatsCounting()
	{
		if (!_statsInProgress || _statsCts is null)
			return;

		_statsCts.Cancel();
		_statsInProgress = false;

		if (_lastStats == default)
			PathsStatsText.Text = string.Empty;
		else
			ShowStats(_lastStats, PathStatsState.Stopped);
	}

	private void ShowStats(PathStats stats, PathStatsState state)
	{
		var key = state switch
		{
			PathStatsState.Counting => "PathsStatsCounting",
			PathStatsState.Stopped => "PathsStatsStopped",
			_ => "PathsStats"
		};

		PathsStatsText.Text = Loc.Format(
			key,
			stats.Files.ToString("N0"),
			stats.Folders.ToString("N0"),
			SizeFormatter.Format(stats.Bytes));
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
		UpdateSdeletePresenceWarning();
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
		UpdateSdeletePresenceWarning();
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

		_options.Executable = Use64BitCheckBox.IsChecked == true ? SDeleteExecutable.Sdelete64 : SDeleteExecutable.Sdelete;
		_options.SdeleteFolderPath = string.IsNullOrWhiteSpace(SdeleteFolderTextBox.Text) ? null : SdeleteFolderTextBox.Text;
		_options.Passes = double.IsNaN(PassesNumberBox.Value) ? 1 : (int)PassesNumberBox.Value;

		_options.TargetPaths = _pathEntries.ToList();
		_options.Recursive = RecurseCheckBox.IsChecked == true;
		_options.RemoveReadOnlyAttribute = RemoveReadOnlyCheckBox.IsChecked == true;

		_options.SelectedDrives = DrivesGridView.SelectedItems
			.Cast<DriveItem>()
			.Select(d => d.DriveLetter)
			.ToList();
		_options.CleanMode = ZeroFillRadio.IsChecked == true ? CleanMode.ZeroFill : CleanMode.CleanFree;
	}

	private void RefreshCommandPreview()
	{
		try
		{
			SyncOptionsFromUI();
			var errors = _options.Validate();

			var previewText = errors.Count > 0
				? Loc.Get("CommandCannotBeBuilt") + "\n" + string.Join('\n', errors.Select(err => "- " + err))
				: "$ " + _options.BuildCommandLine();

			_outputBuffer.Clear();
			_outputBuffer.AppendLine(previewText);

			OutputTextBox.Text = previewText;
		}
		catch (Exception ex)
		{
			var errorText = $"{Loc.Get("CommandBuildInternalError")}\n{ex}";
			_outputBuffer.Clear();
			_outputBuffer.AppendLine(errorText);
			OutputTextBox.Text = errorText;
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
		if (_isRunning)
		{
			PseudoConsoleProcessRunner.RequestStop();
			return;
		}

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

		StopPathStatsCounting();

		_isRunning = true;
		UpdateRunButtonVisual();
		SetStatusRunning();
		SetUiEnabled(false);

		try
		{
			await RunSDeleteAsync();
		}
		finally
		{
			_isRunning = false;
			UpdateRunButtonVisual();
			SetUiEnabled(true);
			RecalculatePathStats();
		}
	}

	private void UpdateRunButtonVisual()
	{
		if (_isRunning)
		{
			_originalButtonPointerOverBackground ??= RunButton.Resources.TryGetValue("AccentButtonBackgroundPointerOver", out var po) ? po as Brush : null;
			_originalButtonPressedBackground ??= RunButton.Resources.TryGetValue("AccentButtonBackgroundPressed", out var pr) ? pr as Brush : null;

			RunButton.Background = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));
			RunButton.Resources["AccentButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 165, 34, 22));
			RunButton.Resources["AccentButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 137, 27, 17));

			RunButton.Content = Loc.Get("StopButton");
		}
		else
		{
			RunButton.ClearValue(Button.BackgroundProperty);

			if (_originalButtonPointerOverBackground is not null)
				RunButton.Resources["AccentButtonBackgroundPointerOver"] = _originalButtonPointerOverBackground;
			else
				RunButton.Resources.Remove("AccentButtonBackgroundPointerOver");

			if (_originalButtonPressedBackground is not null)
				RunButton.Resources["AccentButtonBackgroundPressed"] = _originalButtonPressedBackground;
			else
				RunButton.Resources.Remove("AccentButtonBackgroundPressed");

			RunButton.Content = Loc.Get("RunButton");
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
			? Loc.Get("ConfirmDialogDeleteFilesText")
			: Loc.Get("ConfirmDialogCleanFreeSpaceText");

		const int countdownSeconds = 5;
		var remaining = countdownSeconds;

		ConfirmDialog.IsPrimaryButtonEnabled = false;
		ConfirmDialog.PrimaryButtonText = Loc.Format("ConfirmDialogYesCountdown", remaining);

		var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		timer.Tick += (_, _) =>
		{
			remaining--;
			if (remaining <= 0)
			{
				timer.Stop();
				ConfirmDialog.PrimaryButtonText = Loc.Get("ConfirmDialogYes");
				ConfirmDialog.IsPrimaryButtonEnabled = true;
			}
			else
			{
				ConfirmDialog.PrimaryButtonText = Loc.Format("ConfirmDialogYesCountdown", remaining);
			}
		};
		timer.Start();

		ConfirmDialog.XamlRoot = this.XamlRoot;
		var result = await ConfirmDialog.ShowAsync();

		timer.Stop();

		return result == ContentDialogResult.Primary;
	}

	/// <summary>
	/// Blocks or unblocks the main UI elements (pivot, common options panel)
	/// based on the provided boolean value.
	/// </summary>
	private void SetUiEnabled(bool enabled)
	{
		MainPivot.IsEnabled = enabled;

		Use64BitCheckBox.IsEnabled = enabled;
		PassesNumberBox.IsEnabled = enabled;
		SdeleteFolderTextBox.IsEnabled = enabled;
		BrowseSdeleteFolderButton.IsEnabled = enabled;
	}

	private static readonly System.Text.RegularExpressions.Regex AnsiEscapeRegex =
		new(@"\x1b\[[0-9;?]*[a-zA-Z]|\x1b\][^\a]*\a",
		System.Text.RegularExpressions.RegexOptions.Compiled);

	/// <summary>
	/// Starts the sdelete process with the current options,
	/// captures its output and error streams, and appends them to the OutputTextBox in real-time.
	/// </summary>
	private async Task RunSDeleteAsync()
	{
		SetStatusRunning();
		var dispatcher = DispatcherQueue.GetForCurrentThread();
		RefreshProcessPath();

		var pending = new StringBuilder();

		void OnOutput(string chunk)
		{
			var cleaned = AnsiEscapeRegex.Replace(chunk, string.Empty);

			if (string.IsNullOrEmpty(cleaned)) return;

			pending.Append(cleaned);
			while (true)
			{
				var text = pending.ToString();
				var newlineIndex = text.IndexOfAny(new[] { '\n', '\r' });
				if (newlineIndex < 0) break;

				var line = text[..newlineIndex];
				pending.Clear();
				pending.Append(text[(newlineIndex + 1)..]);

				if (line.Length == 0) continue;

				dispatcher.TryEnqueue(() => AppendOutputLine(line));
			}
		}

		AppendOutputLine(Loc.Get("RunCommandStarted"));

		try
		{
			var exitCode = await PseudoConsoleProcessRunner.RunAsync(
				_options.ResolveExecutablePath(),
				_options.BuildArguments().ToArray(),
				OnOutput);

			if (pending.Length > 0)
				AppendOutputLine(pending.ToString());

			AppendOutputLine(Loc.Format("RunCommandCompleted", exitCode));

			if (exitCode == 0)
				SetStatusCompleted(exitCode);
			else
				SetStatusStopped();
		}
		catch (Exception ex)
		{
			AppendOutputLine(Loc.Format("RunCommandError", ex.Message));
			SetStatusStopped();
		}
	}

	/// <summary>
	/// Refreshes the process environment PATH variable by combining the machine and user PATH variables.
	/// </summary>
	private static void RefreshProcessPath()
	{
		var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
		var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

		var combined = string.Join(
			Path.PathSeparator,
			new[] { machinePath, userPath }.Where(p => !string.IsNullOrWhiteSpace(p)));

		Environment.SetEnvironmentVariable("PATH", combined, EnvironmentVariableTarget.Process);
	}

	private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
	{
		var count = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T typed) return typed;
			var result = FindDescendant<T>(child);
			if (result != null) return result;
		}
		return null;
	}

	private void AppendOutputLine(string line)
	{
		_outputBuffer.AppendLine(line);

		OutputTextBox.Text = _outputBuffer.ToString();

		OutputTextBox.SelectionStart = OutputTextBox.Text.Length;
		OutputTextBox.SelectionLength = 0;

		_outputScrollViewer ??= FindDescendant<ScrollViewer>(OutputTextBox);
    	_outputScrollViewer?.ChangeView(null, _outputScrollViewer.ScrollableHeight, null, disableAnimation: true);
	}

	private void UpdateSdeletePresenceWarning()
	{
		SyncOptionsFromUI();

		var exePath = _options.ResolveExecutablePath() + ".exe";
		bool found;

		if (Path.IsPathRooted(exePath))
		{
			found = File.Exists(exePath);
		}
		else
		{
			found = IsExecutableInPath(exePath);
		}

		if (found)
		{
			SdeleteNotFoundInfoBar.IsOpen = false;
		}
		else
		{
			SdeleteNotFoundInfoBar.Message = Loc.Get("SdeleteNotFound");
			SdeleteNotFoundInfoBar.IsOpen = true;
		}
		System.Diagnostics.Debug.WriteLine($"[SdeleteCheck] found={found}");
	}

	private static bool IsExecutableInPath(string fileName)
	{
		var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
		var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
		var path = string.Join(
			Path.PathSeparator,
			new[] { machinePath, userPath }.Where(p => !string.IsNullOrWhiteSpace(p)));

		foreach (var dir in path.Split(Path.PathSeparator))
		{
			if (string.IsNullOrWhiteSpace(dir)) continue;
			try
			{
				var fullPath = Path.Combine(dir.Trim('"'), fileName);
				if (File.Exists(fullPath)) return true;
			}
			catch { }
		}
		return false;
	}

	// Settings

	private void ApplySettingsToUI(AppSettings settings)
	{
		Use64BitCheckBox.IsChecked = string.Equals(settings.Executable, "Sdelete64", StringComparison.OrdinalIgnoreCase);

		PassesNumberBox.Value = settings.Passes < 1 ? 1 : settings.Passes;

		SdeleteFolderTextBox.Text = settings.SdeleteFolderPath ?? string.Empty;

		RecurseCheckBox.IsChecked = settings.Recursive;
		RemoveReadOnlyCheckBox.IsChecked = settings.RemoveReadOnlyAttribute;

		if (string.Equals(settings.CleanMode, "ZeroFill", StringComparison.OrdinalIgnoreCase))
			ZeroFillRadio.IsChecked = true;
		else
			CleanFreeRadio.IsChecked = true;

		MainPivot.SelectedIndex = string.Equals(settings.Mode, "CleanFreeSpace", StringComparison.OrdinalIgnoreCase)
			? 1
			: 0;
	}

	private AppSettings CollectSettingsFromUI()
	{
		return new AppSettings
		{
			Version = 1,
			Executable = Use64BitCheckBox.IsChecked == true ? "Sdelete64" : "Sdelete",
			SdeleteFolderPath = string.IsNullOrWhiteSpace(SdeleteFolderTextBox.Text)
				? null
				: SdeleteFolderTextBox.Text,
			Passes = double.IsNaN(PassesNumberBox.Value) ? 3 : (int)PassesNumberBox.Value,
			Mode = MainPivot.SelectedIndex == 0 ? "DeleteFiles" : "CleanFreeSpace",
			Recursive = RecurseCheckBox.IsChecked == true,
			RemoveReadOnlyAttribute = RemoveReadOnlyCheckBox.IsChecked == true,
			CleanMode = ZeroFillRadio.IsChecked == true ? "ZeroFill" : "CleanFree"
		};
	}

	public void SaveSettingsOnClose()
	{
		try
		{
			var settings = CollectSettingsFromUI();
			SettingsService.Save(settings);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"SaveSettingsOnClose failed: {ex}");
		}
	}
}
