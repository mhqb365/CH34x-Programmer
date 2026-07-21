using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;

namespace Ch34xProgrammer;

public partial class MainWindow : Window
{
    private const string AppName = "Multi Flash";
    private const string ProjectUrl = "https://github.com/mhqb365/Multi-Flash";
    private const int MaxHexPreviewRows = 4096;
    private const int BytesPerHexRow = 16;
    private const int SearchHitContextBytes = 16;
    private const string MoonIconPath = "M12 3a6 6 0 0 0 9 9a9 9 0 1 1-9-9";
    private const string SunIconPath = "M12 8a4 4 0 1 0 0 8a4 4 0 0 0 0-8 M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M4.93 19.07l1.41-1.41 M17.66 6.34l1.41-1.41";
    private static readonly byte[] WindowsKeyMarker =
    [
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x1D, 0x00, 0x00, 0x00
    ];
    private static readonly Dictionary<string, string> KnownWindowsKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["7H3HT-N36VD-XK866-8RV8Y-39M6M"] = "Win 10 RTM Core OEM:DM, EULA OEM",
        ["TX9XD-98N7V-6WMQ6-BX7FG-H8Q99"] = "Windows 10/11 Home generic install key, Retail channel",
        ["VK7JG-NPHTM-C97JM-9MPGT-3V66T"] = "Windows 10/11 Pro generic install key, Retail channel",
        ["W269N-WFGWX-YVC9B-4J6C9-T83GX"] = "Windows 10/11 Pro generic install key, Volume KMS client",
        ["NPPR9-FWDCX-D2C8J-H872K-2YT43"] = "Windows 10/11 Enterprise generic install key, Volume KMS client",
        ["MH37W-N47XK-V7XM9-C7227-GCQG9"] = "Windows 10/11 Pro N generic install key, Retail channel",
        ["NW6C2-QMPVW-D7KKK-3GKT6-VCFB2"] = "Windows 10/11 Education generic install key, Volume KMS client",
        ["2WH4N-8QGBV-H22JP-CT43Q-MDWWJ"] = "Windows 10/11 Education N generic install key, Volume KMS client",
    };

    private readonly ObservableCollection<HexRow> _rows = [];
    private readonly ObservableCollection<SearchHit> _searchHits = [];
    private readonly List<ChipProfile> _chips =
    [
        new("W25Q80", "SPI", 1024 * 1024, 256, "25xx", "WINBOND", "3.3V", "SPI_NOR"),
        new("W25Q16", "SPI", 2 * 1024 * 1024, 256, "25xx", "WINBOND", "3.3V", "SPI_NOR"),
        new("W25Q32", "SPI", 4 * 1024 * 1024, 256, "25xx", "WINBOND", "3.3V", "SPI_NOR"),
        new("W25Q64", "SPI", 8 * 1024 * 1024, 256, "25xx", "WINBOND", "3.3V", "SPI_NOR"),
        new("W25Q128", "SPI", 16 * 1024 * 1024, 256, "25xx", "WINBOND", "3.3V", "SPI_NOR"),
        new("MX25L1606E", "SPI", 2 * 1024 * 1024, 256, "25xx", "MACRONIX", "3.3V", "SPI_NOR"),
        new("MX25L3206E", "SPI", 4 * 1024 * 1024, 256, "25xx", "MACRONIX", "3.3V", "SPI_NOR"),
        new("MX25L6406E", "SPI", 8 * 1024 * 1024, 256, "25xx", "MACRONIX", "3.3V", "SPI_NOR"),
        new("MX25L12835F", "SPI", 16 * 1024 * 1024, 256, "25xx", "MACRONIX", "3.3V", "SPI_NOR"),
        new("GD25Q16", "SPI", 2 * 1024 * 1024, 256, "25xx", "GIGADEVICE", "3.3V", "SPI_NOR"),
        new("GD25Q32", "SPI", 4 * 1024 * 1024, 256, "25xx", "GIGADEVICE", "3.3V", "SPI_NOR"),
        new("GD25Q64", "SPI", 8 * 1024 * 1024, 256, "25xx", "GIGADEVICE", "3.3V", "SPI_NOR"),
        new("GD25Q128", "SPI", 16 * 1024 * 1024, 256, "25xx", "GIGADEVICE", "3.3V", "SPI_NOR"),
        new("24C02", "I2C", 256, 8, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C04", "I2C", 512, 16, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C08", "I2C", 1024, 16, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C16", "I2C", 2048, 16, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C32", "I2C", 4096, 32, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C64", "I2C", 8192, 32, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C128", "I2C", 16384, 64, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C256", "I2C", 32768, 64, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("24C512", "I2C", 65536, 128, "24xx", "GENERIC", "5V/3.3V", "I2C_EEPROM"),
        new("93C46", "Microwire", 128, 16, "93xx", "GENERIC", "5V/3.3V", "MICROWIRE"),
        new("93C86", "Microwire", 2048, 16, "93xx", "GENERIC", "5V/3.3V", "MICROWIRE")
    ];

    private readonly List<IcCandidate> _icCatalog = [];
    private readonly DispatcherTimer _programmerMonitorTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private IChipProgrammer _programmer = new MockCh34xProgrammer();
    private string _activeProgrammerKey = "none";
    private byte[] _buffer = [];
    private int _previewStartOffset;
    private int _currentOffset;
    private bool _isBusy;
    private bool _isApplyingDetectedChip;
    private bool _isSearching;
    private bool _updatingHexScrollBar;
    private ReplaceDialog? _replaceDialog;

    public MainWindow()
    {
        InitializeComponent();
        _icCatalog = BuildIcCatalog();
        SearchHitsGrid.ItemsSource = _searchHits;
        HexEditor.SetBuffer(_buffer, OnHexCellChanged);
        UpdateHexScrollBar();
        Title = $"{AppName} v{AppVersion}";
        AppendLog($"{AppName} v{AppVersion}");

        _isApplyingDetectedChip = true;
        try
        {
            LoadControls();
        }
        finally
        {
            _isApplyingDetectedChip = false;
        }

        ResizeBuffer(_chips[0].SizeBytes, fill: 0xFF);
        UpdateDeviceInfo(_chips[0]);
        UpdateProgrammerControls();

        _programmerMonitorTimer.Tick += ProgrammerMonitorTimer_Tick;
        Loaded += MainWindow_Loaded;
    }

    private void LoadControls()
    {
        ChipCombo.ItemsSource = _chips;
        ChipCombo.DisplayMemberPath = nameof(ChipProfile.Name);
        ChipCombo.SelectedIndex = 0;

        SizeCombo.ItemsSource = new[]
        {
            new SizeOption("256 B", 256),
            new SizeOption("4 KB", 4096),
            new SizeOption("32 KB", 32768),
            new SizeOption("1 MB", 1024 * 1024),
            new SizeOption("2 MB", 2 * 1024 * 1024),
            new SizeOption("4 MB", 4 * 1024 * 1024),
            new SizeOption("8 MB", 8 * 1024 * 1024),
            new SizeOption("16 MB", 16 * 1024 * 1024)
        };
        SizeCombo.DisplayMemberPath = nameof(SizeOption.Label);

        PageCombo.ItemsSource = new[] { "8", "16", "32", "64", "128", "256" };
        CommandCombo.ItemsSource = new[] { "25xx", "24xx", "93xx", "Custom" };
    }

    private void ChipCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ChipCombo.SelectedItem is not ChipProfile chip)
        {
            return;
        }

        SelectSize(chip.SizeBytes);
        PageCombo.SelectedItem = chip.PageSize.ToString();
        CommandCombo.SelectedItem = chip.CommandSet;
        ResizeBuffer(chip.SizeBytes, fill: 0xFF);
        UpdateDeviceInfo(chip);
        if (!_isApplyingDetectedChip)
        {
            AppendLog($"Selected {chip.Name}: {chip.Protocol}, {FormatBytes(chip.SizeBytes)}, page {chip.PageSize}");
        }
    }

    private void SizeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SizeCombo.SelectedItem is SizeOption size && _buffer.Length != size.Bytes)
        {
            ResizeBuffer(size.Bytes, fill: 0xFF);
            AppendLog($"Buffer resized to {FormatBytes(size.Bytes)}");
        }
    }

    private void SelectSize(int bytes)
    {
        foreach (var item in SizeCombo.Items.OfType<SizeOption>())
        {
            if (item.Bytes == bytes)
            {
                SizeCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void ResizeBuffer(int size, byte fill)
    {
        _buffer = new byte[size];
        if (fill != 0)
        {
            Array.Fill(_buffer, fill);
        }

        RebuildRows();
        UpdateStatus();
    }

    private void RebuildRows(int startOffset = 0)
    {
        _rows.Clear();
        if (_buffer.Length == 0)
        {
            _previewStartOffset = 0;
            HexEditor.SetBuffer(_buffer, OnHexCellChanged);
            UpdateHexScrollBar();
            return;
        }

        _previewStartOffset = AlignOffset(Math.Clamp(startOffset, 0, _buffer.Length - 1));
        var maxPreviewBytes = MaxHexPreviewRows * BytesPerHexRow;
        var endOffset = Math.Min(_buffer.Length, _previewStartOffset + maxPreviewBytes);
        for (var offset = _previewStartOffset; offset < endOffset; offset += BytesPerHexRow)
        {
            _rows.Add(new HexRow(_buffer, offset, OnHexCellChanged));
        }

        HexEditor.SetBuffer(_buffer, OnHexCellChanged);
        UpdateHexScrollBar();
    }

    private void OnHexCellChanged(int offset, byte value)
    {
        if ((uint)offset < _buffer.Length)
        {
            _buffer[offset] = value;
            var rowIndex = (offset - _previewStartOffset) / BytesPerHexRow;
            if ((uint)rowIndex < _rows.Count)
            {
                _rows[rowIndex].RefreshAscii();
            }
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        if (!await CheckForUpdatesOnStartupAsync())
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
        await ProbeProgrammerAsync(logWhenChanged: true);

        _programmerMonitorTimer.Start();
    }

    private async Task<bool> CheckForUpdatesOnStartupAsync()
    {
        OperationStatusText.Text = "Checking update";
        OperationProgress.IsIndeterminate = true;
        try
        {
            var result = await UpdateService.CheckLatestReleaseAsync();
            if (result.Status != UpdateCheckStatus.UpdateAvailable || result.Release is null)
            {
                return true;
            }

            var updateNow = MessageBox.Show(
                this,
                $"A new version is available: {result.DisplayLatestVersion}\nCurrent version: {UpdateService.DisplayCurrentVersion}\n\nUpdate now?",
                "Update available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information) == MessageBoxResult.Yes;
            if (!updateNow)
            {
                AppendLog($"Update skipped: {result.DisplayLatestVersion}");
                return true;
            }

            using var cts = new CancellationTokenSource();
            OperationProgress.IsIndeterminate = false;
            OperationProgress.Value = 0;
            var progress = new Progress<UpdateProgressInfo>(info =>
            {
                OperationProgress.IsIndeterminate = info.State != UpdateProgressState.Downloading;
                OperationProgress.Value = Math.Clamp(info.Percentage, 0, 100);
                OperationStatusText.Text = info.State switch
                {
                    UpdateProgressState.Downloading => "Downloading update",
                    UpdateProgressState.Extracting => "Extracting update",
                    UpdateProgressState.Preparing => "Preparing update",
                    _ => "Updating"
                };
            });

            AppendLog($"Downloading update {result.DisplayLatestVersion}");
            var update = await UpdateService.DownloadAndPrepareUpdateAsync(result.Release, progress, cts.Token);
            AppendLog("Installing update");
            UpdateService.InstallPreparedUpdate(update);
            return false;
        }
        catch (Exception ex)
        {
            AppendLog($"Update check skipped: {ex.Message}");
            return true;
        }
        finally
        {
            OperationProgress.IsIndeterminate = false;
            OperationProgress.Value = 0;
            OperationStatusText.Text = "Ready";
        }
    }

    private async void ProgrammerMonitorTimer_Tick(object? sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        _programmerMonitorTimer.Stop();
        try
        {
            await ProbeProgrammerAsync(logWhenChanged: true);
        }
        finally
        {
            _programmerMonitorTimer.Start();
        }
    }

    private async Task ProbeProgrammerAsync(bool logWhenChanged)
    {
        await Task.Yield();
        var t48Detected = T48SDKProgrammer.CanOpenDevice();
        var ch347Detected = Ch347NativeProgrammer.IsAvailable && Ch347NativeProgrammer.CanOpenDevice();
        var chDetected = ChNativeProgrammer.IsAvailable && ChNativeProgrammer.CanOpenDevice();
        ApplyProgrammerDetection(t48Detected, ch347Detected, chDetected, logWhenChanged, forceLog: false);
    }

    private void ApplyProgrammerDetection(bool t48Detected, bool ch347Detected, bool chDetected, bool logWhenChanged, bool forceLog)
    {
        if (t48Detected)
        {
            var changed = _activeProgrammerKey != "t48";
            _programmer = new T48SDKProgrammer();
            _activeProgrammerKey = "t48";
            HardwareStatusText.Text = "XGecu T48 connected";
            UpdateProgrammerControls();
            if (forceLog || changed && logWhenChanged)
            {
                AppendLog("XGecu T48 connected. Active backend: XGecu T48 SDK");
            }
            return;
        }

        if (ch347Detected)
        {
            var changed = _activeProgrammerKey != "ch347";
            _programmer = new Ch347NativeProgrammer();
            _activeProgrammerKey = "ch347";
            HardwareStatusText.Text = "CH347 connected";
            UpdateProgrammerControls();
            if (forceLog || changed && logWhenChanged)
            {
                AppendLog("CH347 connected. Active backend: CH347 native DLL");
            }
            return;
        }

        if (chDetected)
        {
            var changed = _activeProgrammerKey != "ch341";
            _programmer = new ChNativeProgrammer();
            _activeProgrammerKey = "ch341";
            HardwareStatusText.Text = "CH341 connected";
            UpdateProgrammerControls();
            if (forceLog || changed && logWhenChanged)
            {
                AppendLog("CH341 connected. Active backend: CH341 native DLL");
            }
            return;
        }

        var wasConnected = _activeProgrammerKey != "none";
        _programmer = new MockCh34xProgrammer();
        _activeProgrammerKey = "none";
        HardwareStatusText.Text = "Programmer disconnected";
        UpdateProgrammerControls();
        if (forceLog || wasConnected && logWhenChanged)
        {
            AppendLog("Programmer disconnected");
        }
    }

    private async void ReadId_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureProgrammerAvailable("Detect IC"))
        {
            return;
        }

        await DetectIcAsync(logLifecycle: true, autoApplySingle: true, openCatalogOnMiss: true);
    }

    private Task DetectIcAsync(bool logLifecycle, bool autoApplySingle, bool openCatalogOnMiss) =>
        RunOperationAsync("Read ID", async progress =>
        {
            var id = await _programmer.ReadIdAsync(CurrentChip(), progress);
            AppendLog($"IC ID: {BitConverter.ToString(id).Replace("-", " ")}");
            ShowChipSelectionForId(id, autoApplySingle, openCatalogOnMiss);
        }, logLifecycle: logLifecycle);

    private bool HasProgrammer => _programmer is not MockCh34xProgrammer;

    private void UpdateProgrammerControls()
    {
        var enabled = HasProgrammer;
        ReadIdButton.IsEnabled = enabled;
        ReadIdMenuItem.IsEnabled = enabled;
        ReadButton.IsEnabled = enabled;
        ReadChipMenuItem.IsEnabled = enabled;
        WriteButton.IsEnabled = enabled;
        WriteChipMenuItem.IsEnabled = enabled;
        VerifyButton.IsEnabled = enabled;
        VerifyChipMenuItem.IsEnabled = enabled;
        EraseButton.IsEnabled = enabled;
        EraseChipMenuItem.IsEnabled = enabled;
        ReadVerifyScriptMenuItem.IsEnabled = enabled;
        EraseWriteVerifyScriptMenuItem.IsEnabled = enabled;
    }

    private bool EnsureProgrammerAvailable(string operationName)
    {
        if (HasProgrammer)
        {
            return true;
        }

        AppendLog($"{operationName} skipped: no programmer found");
        return false;
    }

    private void SearchIc_Click(object sender, RoutedEventArgs e)
    {
        ShowChipSelection(_icCatalog, "Search IC", null);
    }

    private async void ReadChip_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureProgrammerAvailable("Read chip"))
        {
            return;
        }

        await RunOperationAsync("Read chip", _buffer.Length, async progress =>
        {
            var startAddress = ParseStartAddress();
            AppendLog($"Read request: {FormatBytes(_buffer.Length)} from 0x{startAddress:X6}");
            _buffer = await _programmer.ReadAsync(CurrentChip(), startAddress, _buffer.Length, progress);
            RebuildRows();
            UpdateStatus();
        });
    }

    private async void WriteChip_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureProgrammerAvailable("Write chip"))
        {
            return;
        }

        await RunOperationAsync("Write chip", _buffer.Length, async progress =>
        {
            var chip = CurrentChip();
            var startAddress = ParseStartAddress();
            var skipBlankPages = SkipBlankPagesCheckBox.IsChecked == true;
            AppendLog($"Write request: {FormatBytes(_buffer.Length)} to 0x{startAddress:X6}{(skipBlankPages ? " (skip FF pages)" : "")}");
            await UnprotectIfRequestedAsync(chip, progress);
            await _programmer.WriteAsync(chip, startAddress, _buffer, progress, skipBlankPages);
        });
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureProgrammerAvailable("Verify"))
        {
            return;
        }

        await RunOperationAsync("Verify", _buffer.Length, async progress =>
        {
            var startAddress = ParseStartAddress();
            AppendLog($"Verify request: {FormatBytes(_buffer.Length)} at 0x{startAddress:X6}");
            var ok = await _programmer.VerifyAsync(CurrentChip(), startAddress, _buffer, progress);
            AppendLog(ok ? "Verify OK" : "Verify failed");
        });
    }

    private async void Erase_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureProgrammerAvailable("Erase chip"))
        {
            return;
        }

        if (MessageBox.Show("Erase selected IC?", "Confirm erase", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await RunOperationAsync("Erase chip", CurrentChip().SizeBytes, async progress =>
        {
            var chip = CurrentChip();
            await UnprotectIfRequestedAsync(chip, progress);
            await _programmer.EraseAsync(chip, progress);
        });
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("Stop requested. Current operation will finish its current block");
    }

    private async void HexSearchPrevious_Click(object sender, RoutedEventArgs e) => await RunSearchAsync(forward: false);

    private async void HexSearchNext_Click(object sender, RoutedEventArgs e) => await RunSearchAsync(forward: true);

    private async void HexSearchAll_Click(object sender, RoutedEventArgs e) => await RunSearchAllAsync();

    private async void WindowsKeySearch_Click(object sender, RoutedEventArgs e) => await RunWindowsKeySearchAsync();

    private async void HexReplace_Click(object sender, RoutedEventArgs e) => await RunReplaceDialogAsync();

    private void HexSearchClear_Click(object sender, RoutedEventArgs e)
    {
        HexSearchBox.Clear();
        HexSearchBox.Focus();
    }

    private void HexSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        HexSearchClearButton.Visibility = string.IsNullOrEmpty(HexSearchBox.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void SearchHitsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SearchHitsGrid.SelectedItem is SearchHit { Offset: >= 0, Length: > 0 } hit)
        {
            ShowSearchResult(hit.Offset, hit.Length);
        }
    }

    private async void HexSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await RunSearchAsync(forward: true);
    }

    private void HexEditor_ScrollChanged(object sender, EventArgs e) => UpdateHexScrollBar();

    private void HexScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingHexScrollBar)
        {
            return;
        }

        HexEditor.SetFirstLine((int)e.NewValue);
    }

    private void UpdateHexScrollBar()
    {
        _updatingHexScrollBar = true;
        try
        {
            HexScrollBar.Maximum = Math.Max(0, HexEditor.TotalLines - HexEditor.VisibleLines);
            HexScrollBar.ViewportSize = HexEditor.VisibleLines;
            HexScrollBar.LargeChange = Math.Max(1, HexEditor.VisibleLines - 1);
            HexScrollBar.SmallChange = 1;
            HexScrollBar.Value = Math.Min(HexScrollBar.Maximum, HexEditor.FirstLine);
        }
        finally
        {
            _updatingHexScrollBar = false;
        }
    }

    private async Task RunSearchAsync(bool forward)
    {
        if (_isSearching)
        {
            AppendLog("Search is already running");
            return;
        }

        var query = HexSearchBox.Text?.Trim() ?? string.Empty;
        var mode = CurrentHexSearchMode();
        _isSearching = true;
        SetSearchControlsEnabled(false);
        try
        {
            await SearchHexViewAsync(mode, query, forward);
        }
        finally
        {
            SetSearchControlsEnabled(true);
            _isSearching = false;
        }
    }

    private async Task RunSearchAllAsync()
    {
        if (_isSearching)
        {
            AppendLog("Search is already running");
            return;
        }

        var query = HexSearchBox.Text?.Trim() ?? string.Empty;
        var mode = CurrentHexSearchMode();
        _isSearching = true;
        SetSearchControlsEnabled(false);
        try
        {
            await SearchAllHexViewAsync(mode, query);
        }
        finally
        {
            SetSearchControlsEnabled(true);
            _isSearching = false;
        }
    }

    private async Task RunWindowsKeySearchAsync()
    {
        if (_isSearching)
        {
            AppendLog("Search is already running");
            return;
        }

        _isSearching = true;
        SetSearchControlsEnabled(false);
        try
        {
            await SearchWindowsKeyMarkerAsync();
        }
        finally
        {
            SetSearchControlsEnabled(true);
            _isSearching = false;
        }
    }

    private Task RunReplaceDialogAsync()
    {
        if (_isSearching)
        {
            AppendLog("Search is already running");
            return Task.CompletedTask;
        }

        var mode = CurrentHexSearchMode();
        if (string.Equals(mode, "Offset", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog("Replace supports Hex and Text modes only");
            return Task.CompletedTask;
        }

        if (_replaceDialog is not null)
        {
            _replaceDialog.Activate();
            return Task.CompletedTask;
        }

        var dialog = new ReplaceDialog(mode, RunReplaceFromDialogAsync)
        {
            Owner = this
        };
        _replaceDialog = dialog;
        dialog.Closed += (_, _) => _replaceDialog = null;
        dialog.Show();
        return Task.CompletedTask;
    }

    private async Task RunReplaceFromDialogAsync(string replacementText, bool replaceAll)
    {
        if (_isSearching)
        {
            AppendLog("Search is already running");
            return;
        }

        _isSearching = true;
        SetSearchControlsEnabled(false);
        try
        {
            await ReplaceHexViewAsync(replaceAll, replacementText);
        }
        finally
        {
            SetSearchControlsEnabled(true);
            _isSearching = false;
        }
    }

    private void SetSearchControlsEnabled(bool enabled)
    {
        HexSearchBox.IsEnabled = enabled;
        HexSearchModeCombo.IsEnabled = enabled;
        HexSearchPreviousButton.IsEnabled = enabled;
        HexSearchAllButton.IsEnabled = enabled;
        HexSearchNextButton.IsEnabled = enabled;
        HexReplaceButton.IsEnabled = enabled;
        WindowsKeySearchButton.IsEnabled = enabled;
    }

    private string CurrentHexSearchMode() => HexSearchModeCombo.SelectedItem as string ?? "Offset";

    private async Task SearchHexViewAsync(string mode, string query, bool forward)
    {
        try
        {
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            var result = await TryResolveSearchOffsetAsync(mode, query, forward);
            if (!result.Found)
            {
                AppendLog(result.Message);
                ShowSearchHits([], query, result.Message);
                return;
            }

            var offset = result.Offset;
            if ((uint)offset >= _buffer.Length)
            {
                AppendLog($"Offset 0x{offset:X6} is outside buffer range 0x000000-0x{Math.Max(0, _buffer.Length - 1):X6}");
                return;
            }

            if (string.Equals(mode, "Offset", StringComparison.OrdinalIgnoreCase))
            {
                ViewOffset(offset);
                return;
            }

            ShowSearchHits([offset], query, $"{query}: 1 match");
            ShowSearchResult(offset, result.Length);
        }
        catch (Exception ex)
        {
            AppendLog($"Search failed: {ex.Message}");
        }
    }

    private async Task SearchAllHexViewAsync(string mode, string query)
    {
        try
        {
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            if (string.Equals(mode, "Offset", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("Search all supports Hex and Text modes only");
                return;
            }

            byte[] pattern;
            string label;
            if (string.Equals(mode, "Text", StringComparison.OrdinalIgnoreCase))
            {
                pattern = Encoding.ASCII.GetBytes(query);
                label = $"Text \"{query}\"";
            }
            else
            {
                if (!TryParseHexPattern(query, out pattern))
                {
                    AppendLog($"Invalid hex pattern: {query}");
                    return;
                }

                label = $"Hex {FormatHexPattern(pattern)}";
            }

            if (pattern.Length == 0)
            {
                AppendLog($"Nothing to search: {query}");
                return;
            }

            var buffer = _buffer;
            AppendLog($"Searching all {FormatBytes(buffer.Length)}...");
            var offsets = await Task.Run(() => string.Equals(mode, "Text", StringComparison.OrdinalIgnoreCase)
                ? FindAllAsciiText(buffer, pattern)
                : FindAllBytes(buffer, pattern));

            if (offsets.Count == 0)
            {
                AppendLog($"{label} not found");
                ShowSearchHits([], query, $"{label} not found");
                return;
            }

            AppendLog($"{label}: {offsets.Count} match(es); see Search tab");

            ShowSearchHits(offsets, query, $"{label}: {offsets.Count} match(es)");
            ShowSearchResult(offsets[0], pattern.Length);
        }
        catch (Exception ex)
        {
            AppendLog($"Search all failed: {ex.Message}");
        }
    }

    private async Task ReplaceHexViewAsync(bool replaceAll, string replacementText)
    {
        try
        {
            var mode = CurrentHexSearchMode();
            var query = HexSearchBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            if (string.Equals(mode, "Offset", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("Replace supports Hex and Text modes only");
                return;
            }

            if (!TryBuildSearchAndReplacement(mode, query, replacementText, out var pattern, out var replacement, out var label))
            {
                return;
            }

            if (replacement.Length > pattern.Length)
            {
                AppendLog("Replacement cannot be longer than the search pattern");
                return;
            }

            var replaceBytes = PadReplacement(replacement, pattern.Length);
            var offsets = replaceAll
                ? await Task.Run(() => string.Equals(mode, "Text", StringComparison.OrdinalIgnoreCase)
                    ? FindAllAsciiText(_buffer, pattern)
                    : FindAllBytes(_buffer, pattern))
                : await FindSingleReplaceOffsetAsync(mode, pattern);
            if (replaceAll)
            {
                offsets = RemoveOverlappingMatches(offsets, pattern.Length);
            }

            if (offsets.Count == 0)
            {
                AppendLog($"{label} not found");
                ShowSearchHits([], query, $"{label} not found");
                return;
            }

            var changed = 0;
            foreach (var offset in offsets)
            {
                if (HexEditor.ReplaceBytes(offset, replaceBytes))
                {
                    changed++;
                }
            }

            ShowSearchHits(offsets, query, $"{label}: {offsets.Count} replace candidate(s)");
            ShowSearchResult(offsets[0], pattern.Length, logFound: false);
            _currentOffset = replaceAll
                ? offsets[^1]
                : Math.Min(_buffer.Length - 1, offsets[0] + pattern.Length - 1);
            AppendLog(replaceAll
                ? $"Replaced {changed} of {offsets.Count} match(es)"
                : changed > 0 ? $"Replaced at 0x{offsets[0]:X6}" : $"Match at 0x{offsets[0]:X6} already has replacement value");
        }
        catch (Exception ex)
        {
            AppendLog($"Replace failed: {ex.Message}");
        }
    }

    private async Task SearchWindowsKeyMarkerAsync()
    {
        try
        {
            var buffer = _buffer;
            AppendLog("Searching Windows key");
            var candidates = await Task.Run(() => FindWindowsKeyCandidates(buffer));
            if (candidates.Count == 0)
            {
                AppendLog("Windows key not found");
                ShowSearchHits([], "Windows key", "Windows key not found");
                return;
            }

            foreach (var candidate in candidates)
            {
                AppendLog($"Windows key found at 0x{candidate.Offset:X6}: {candidate.Key} ({candidate.Description})");
            }

            ShowWindowsKeyHits(candidates);
            ShowSearchResult(candidates[0].Offset, candidates[0].Length, logFound: false);
        }
        catch (Exception ex)
        {
            AppendLog($"Windows key search failed: {ex.Message}");
        }
    }

    private static List<WindowsKeyCandidate> FindWindowsKeyCandidates(byte[] buffer)
    {
        var candidates = new List<WindowsKeyCandidate>();
        var knownOffsets = new HashSet<int>();

        foreach (var markerOffset in FindAllBytes(buffer, WindowsKeyMarker))
        {
            if (TryFindWindowsProductKey(buffer, markerOffset + WindowsKeyMarker.Length, 256, out var key, out var keyOffset))
            {
                AddWindowsKeyCandidate(candidates, knownOffsets, "Hex marker", keyOffset, key);
            }
        }

        foreach (var msdmOffset in FindAllAsciiText(buffer, Encoding.ASCII.GetBytes("MSDM")))
        {
            if (TryFindWindowsProductKey(buffer, msdmOffset, 512, out var key, out var keyOffset))
            {
                AddWindowsKeyCandidate(candidates, knownOffsets, "ACPI MSDM", keyOffset, key);
            }
        }

        foreach (var anchor in new[] { "Windows", "Product", "ProductKey", "DigitalProductId" })
        {
            foreach (var anchorOffset in FindAllAsciiText(buffer, Encoding.ASCII.GetBytes(anchor)))
            {
                if (TryFindWindowsProductKey(buffer, anchorOffset, 768, out var key, out var keyOffset))
                {
                    AddWindowsKeyCandidate(candidates, knownOffsets, $"Near {anchor}", keyOffset, key);
                }
            }
        }

        foreach (var candidate in FindAllWindowsProductKeys(buffer))
        {
            AddWindowsKeyCandidate(candidates, knownOffsets, "Direct pattern", candidate.Offset, candidate.Key);
        }

        return candidates
            .OrderBy(x => x.Method == "Hex marker" ? 0 : x.Method == "ACPI MSDM" ? 1 : x.Method.StartsWith("Near ", StringComparison.Ordinal) ? 2 : 3)
            .ThenBy(x => x.Offset)
            .ToList();
    }

    private static void AddWindowsKeyCandidate(List<WindowsKeyCandidate> candidates, HashSet<int> knownOffsets, string method, int offset, string key)
    {
        if (knownOffsets.Add(offset))
        {
            candidates.Add(new WindowsKeyCandidate(method, offset, key, 29, DescribeWindowsKey(key, method)));
        }
    }

    private static string DescribeWindowsKey(string key, string method)
    {
        if (TryGetPidGenXDescription(key, out var pidGenXDescription))
        {
            return pidGenXDescription;
        }

        if (KnownWindowsKeys.TryGetValue(key, out var known))
        {
            return known;
        }

        if (method is "Hex marker" or "ACPI MSDM")
        {
            return "likely OEM:DM embedded key";
        }

        if (method.Contains("DigitalProductId", StringComparison.OrdinalIgnoreCase))
        {
            return "likely installed Windows product key";
        }

        if (method.StartsWith("Near ", StringComparison.Ordinal))
        {
            return "possible Windows product key";
        }

        return "product key candidate";
    }

    private static bool TryGetPidGenXDescription(string key, out string description)
    {
        description = string.Empty;
        var pkeyConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "spp", "tokens", "pkeyconfig", "pkeyconfig.xrm-ms");
        if (!File.Exists(pkeyConfig))
        {
            return false;
        }

        var buffer = new byte[0x04F8];
        buffer[0] = 0xF8;
        buffer[1] = 0x04;
        var nativeBuffer = IntPtr.Zero;
        try
        {
            nativeBuffer = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, nativeBuffer, buffer.Length);

            var result = PidGenX(key, pkeyConfig, "00000", 0, IntPtr.Zero, IntPtr.Zero, nativeBuffer);
            if (result != 0)
            {
                return false;
            }

            Marshal.Copy(nativeBuffer, buffer, 0, buffer.Length);
            var strings = ExtractPrintableUnicodeStrings(buffer).ToList();
            var channel = ReadUnicodeString(buffer, 1016, 128);
            var edition = strings.FirstOrDefault(IsLikelyWindowsEdition);
            var eula = strings.FirstOrDefault(x => x is "OEM" or "Retail" or "Volume");
            if (string.IsNullOrWhiteSpace(channel) && edition is null && eula is null)
            {
                return false;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(edition))
            {
                parts.Add(edition);
            }

            if (!string.IsNullOrWhiteSpace(channel))
            {
                parts.Add(channel);
            }

            if (!string.IsNullOrWhiteSpace(eula))
            {
                parts.Add($"EULA {eula}");
            }

            description = string.Join(", ", parts);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
    }

    private static IEnumerable<string> ExtractPrintableUnicodeStrings(byte[] buffer)
    {
        var text = Encoding.Unicode.GetString(buffer);
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch is >= ' ' and <= '~')
            {
                builder.Append(ch);
                continue;
            }

            if (builder.Length >= 3)
            {
                yield return builder.ToString();
            }

            builder.Clear();
        }

        if (builder.Length >= 3)
        {
            yield return builder.ToString();
        }
    }

    private static string ReadUnicodeString(byte[] buffer, int offset, int byteCount) =>
        offset + byteCount > buffer.Length ? string.Empty : Encoding.Unicode.GetString(buffer, offset, byteCount).TrimEnd('\0').Trim();

    private static bool IsLikelyWindowsEdition(string value)
    {
        if (value.Contains('-') || value.Contains('.') || Guid.TryParse(value, out _))
        {
            return false;
        }

        return value.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Professional", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Education", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Server", StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("pidgenx.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
    private static extern int PidGenX(
        string productKey,
        string pkeyConfigPath,
        string mpcId,
        int unknown,
        IntPtr productId,
        IntPtr digitalProductId,
        IntPtr digitalProductId4);

    private void ShowWindowsKeyHits(IReadOnlyList<WindowsKeyCandidate> candidates)
    {
        _searchHits.Clear();
        foreach (var candidate in candidates)
        {
            _searchHits.Add(CreateSearchHit(candidate.Offset, candidate.Length));
        }
    }

    private static List<WindowsKeyCandidate> FindAllWindowsProductKeys(byte[] buffer)
    {
        var candidates = new List<WindowsKeyCandidate>();
        for (var offset = 0; offset <= buffer.Length - 29; offset++)
        {
            if (!LooksLikeWindowsProductKey(buffer, offset))
            {
                continue;
            }

            var key = ReadWindowsProductKey(buffer, offset);
            candidates.Add(new WindowsKeyCandidate("Direct pattern", offset, key, 29, DescribeWindowsKey(key, "Direct pattern")));
            offset += 28;
        }

        return candidates;
    }

    private static bool TryFindWindowsProductKey(byte[] buffer, int startOffset, int maxDistance, out string key, out int keyOffset)
    {
        key = string.Empty;
        keyOffset = -1;
        var searchEnd = Math.Min(buffer.Length, startOffset + maxDistance);
        for (var offset = Math.Max(0, startOffset); offset <= searchEnd - 29; offset++)
        {
            if (!LooksLikeWindowsProductKey(buffer, offset))
            {
                continue;
            }

            key = ReadWindowsProductKey(buffer, offset);
            keyOffset = offset;
            return true;
        }

        return false;
    }

    private static bool LooksLikeWindowsProductKey(byte[] buffer, int offset)
    {
        for (var i = 0; i < 29; i++)
        {
            var value = buffer[offset + i];
            if (i is 5 or 11 or 17 or 23)
            {
                if (value != '-')
                {
                    return false;
                }

                continue;
            }

            if (!IsWindowsProductKeyChar(value))
            {
                return false;
            }
        }

        return IsPlausibleWindowsProductKey(ReadWindowsProductKey(buffer, offset));
    }

    private static bool IsWindowsProductKeyChar(byte value) =>
        value is >= (byte)'A' and <= (byte)'Z' ||
        value is >= (byte)'a' and <= (byte)'z' ||
        value is >= (byte)'0' and <= (byte)'9';

    private static string ReadWindowsProductKey(byte[] buffer, int offset) =>
        Encoding.ASCII.GetString(buffer, offset, 29).ToUpperInvariant();

    private static bool IsPlausibleWindowsProductKey(string key)
    {
        var compact = key.Replace("-", string.Empty, StringComparison.Ordinal);
        if (!compact.Any(char.IsLetter) || !compact.Any(char.IsDigit))
        {
            return false;
        }

        var groups = key.Split('-');
        return groups.Length == 5 &&
               groups.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1 &&
               compact.Distinct().Count() >= 6;
    }

    private async Task<SearchResult> TryResolveSearchOffsetAsync(string mode, string query, bool forward)
    {
        switch (mode)
        {
            case "Offset":
                if (TryParseOffset(query, out var parsedOffset))
                {
                    return SearchResult.Success(parsedOffset, 1);
                }

                return SearchResult.Fail($"Invalid offset: {query}");

            case "Text":
                return await SearchTextAsync(query, forward, $"Text not found: {query}");

            default:
                if (!TryParseHexPattern(query, out var pattern))
                {
                    return SearchResult.Fail($"Invalid hex pattern: {query}");
                }

                return await SearchPatternAsync(pattern, forward, $"Hex pattern not found: {query}");
        }
    }

    private async Task<SearchResult> SearchPatternAsync(byte[] pattern, bool forward, string notFoundMessage)
    {
        if (pattern.Length == 0)
        {
            return SearchResult.Fail(notFoundMessage);
        }

        var buffer = _buffer;
        var startOffset = Math.Clamp(_currentOffset + (forward ? 1 : -1), 0, Math.Max(0, buffer.Length - 1));
        AppendLog($"Searching {FormatBytes(buffer.Length)}...");
        var offset = await Task.Run(() => FindBytes(buffer, pattern, startOffset, forward));
        if (offset < 0 && startOffset != 0)
        {
            offset = await Task.Run(() => FindBytes(buffer, pattern, forward ? 0 : buffer.Length - 1, forward));
        }

        return offset >= 0 ? SearchResult.Success(offset, pattern.Length) : SearchResult.Fail(notFoundMessage);
    }

    private async Task<SearchResult> SearchTextAsync(string text, bool forward, string notFoundMessage)
    {
        var pattern = Encoding.ASCII.GetBytes(text);
        if (pattern.Length == 0)
        {
            return SearchResult.Fail(notFoundMessage);
        }

        var buffer = _buffer;
        var startOffset = Math.Clamp(_currentOffset + (forward ? 1 : -1), 0, Math.Max(0, buffer.Length - 1));
        AppendLog($"Searching {FormatBytes(buffer.Length)}...");
        var offset = await Task.Run(() => FindAsciiText(buffer, pattern, startOffset, forward));
        if (offset < 0 && startOffset != 0)
        {
            offset = await Task.Run(() => FindAsciiText(buffer, pattern, forward ? 0 : buffer.Length - 1, forward));
        }

        return offset >= 0 ? SearchResult.Success(offset, pattern.Length) : SearchResult.Fail(notFoundMessage);
    }

    private async Task<List<int>> FindSingleReplaceOffsetAsync(string mode, byte[] pattern)
    {
        var selectedOffset = HexEditor.SelectedOffset;
        if ((uint)selectedOffset < _buffer.Length && selectedOffset <= _buffer.Length - pattern.Length)
        {
            var matchesSelected = string.Equals(mode, "Text", StringComparison.OrdinalIgnoreCase)
                ? AsciiEqualsIgnoreCase(_buffer, pattern, selectedOffset)
                : _buffer.AsSpan(selectedOffset, pattern.Length).SequenceEqual(pattern);
            if (matchesSelected)
            {
                return [selectedOffset];
            }
        }

        var result = string.Equals(mode, "Text", StringComparison.OrdinalIgnoreCase)
            ? await SearchTextAsync(Encoding.ASCII.GetString(pattern), forward: true, $"{mode} not found")
            : await SearchPatternAsync(pattern, forward: true, "Hex pattern not found");
        return result.Found ? [result.Offset] : [];
    }

    private bool TryBuildSearchAndReplacement(string mode, string query, string replacementText, out byte[] pattern, out byte[] replacement, out string label)
    {
        pattern = [];
        replacement = [];
        label = string.Empty;

        if (string.Equals(mode, "Text", StringComparison.OrdinalIgnoreCase))
        {
            pattern = Encoding.ASCII.GetBytes(query);
            replacement = Encoding.ASCII.GetBytes(replacementText);
            label = $"Text \"{query}\"";
        }
        else
        {
            if (!TryParseHexPattern(query, out pattern))
            {
                AppendLog($"Invalid hex pattern: {query}");
                return false;
            }

            if (!TryParseHexPattern(replacementText, out replacement))
            {
                AppendLog($"Invalid replacement hex: {replacementText}");
                return false;
            }

            label = $"Hex {FormatHexPattern(pattern)}";
        }

        if (pattern.Length == 0)
        {
            AppendLog($"Nothing to replace: {query}");
            return false;
        }

        if (replacement.Length == 0)
        {
            AppendLog("Replacement is empty");
            return false;
        }

        return true;
    }

    private static byte[] PadReplacement(byte[] replacement, int length)
    {
        if (replacement.Length == length)
        {
            return replacement;
        }

        var padded = new byte[length];
        Buffer.BlockCopy(replacement, 0, padded, 0, replacement.Length);
        return padded;
    }

    private static List<int> RemoveOverlappingMatches(IEnumerable<int> offsets, int length)
    {
        var filtered = new List<int>();
        var nextAllowed = 0;
        foreach (var offset in offsets)
        {
            if (offset < nextAllowed)
            {
                continue;
            }

            filtered.Add(offset);
            nextAllowed = offset + length;
        }

        return filtered;
    }

    private void ShowSearchResult(int offset, int length = 1, bool logFound = true)
    {
        _currentOffset = offset;
        HexEditor.SelectRange(offset, length);
        if (logFound)
        {
            AppendLog($"Found at 0x{offset:X6}");
        }
    }

    private void ShowSearchHits(IReadOnlyList<int> offsets, string query, string status)
    {
        _searchHits.Clear();
        if (offsets.Count == 0)
        {
            _searchHits.Add(SearchHit.Message(status));
            return;
        }

        var length = Math.Max(1, CurrentHexSearchMode().Equals("Text", StringComparison.OrdinalIgnoreCase)
            ? Encoding.ASCII.GetByteCount(query)
            : TryParseHexPattern(query, out var pattern) ? pattern.Length : 1);
        foreach (var offset in offsets)
        {
            _searchHits.Add(CreateSearchHit(offset, length));
        }
    }

    private SearchHit CreateSearchHit(int offset, int length)
    {
        var contextStart = Math.Max(0, offset - SearchHitContextBytes);
        var contextEnd = Math.Min(_buffer.Length, offset + length + SearchHitContextBytes);
        var span = _buffer.AsSpan(contextStart, contextEnd - contextStart).ToArray();
        var hex = string.Join(" ", span.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
        var text = new string(span.Select(b => b is >= 32 and <= 126 ? (char)b : '.').ToArray());
        return new SearchHit(offset, length, $"0x{offset:X6}", hex, text);
    }

    private void ViewOffset(int offset)
    {
        _currentOffset = offset;
        RebuildRows(offset);
        UpdateStatus();
        HexEditor.ScrollToOffset(offset);
        AppendLog($"Viewing 0x{_previewStartOffset:X6}");
    }

    private static int AlignOffset(int offset) => offset / BytesPerHexRow * BytesPerHexRow;

    private static int FindBytes(byte[] buffer, byte[] pattern, int startOffset, bool forward)
    {
        if (pattern.Length == 0 || pattern.Length > buffer.Length)
        {
            return -1;
        }

        startOffset = Math.Clamp(startOffset, 0, buffer.Length - 1);
        if (forward)
        {
            var index = buffer.AsSpan(startOffset).IndexOf(pattern);
            return index < 0 ? -1 : startOffset + index;
        }

        for (var offset = Math.Min(startOffset, buffer.Length - pattern.Length); offset >= 0; offset--)
        {
            if (buffer.AsSpan(offset, pattern.Length).SequenceEqual(pattern))
            {
                return offset;
            }
        }

        return -1;
    }

    private static List<int> FindAllBytes(byte[] buffer, byte[] pattern)
    {
        var offsets = new List<int>();
        if (pattern.Length == 0 || pattern.Length > buffer.Length)
        {
            return offsets;
        }

        var offset = 0;
        while (offset <= buffer.Length - pattern.Length)
        {
            var index = buffer.AsSpan(offset).IndexOf(pattern);
            if (index < 0)
            {
                break;
            }

            var absolute = offset + index;
            offsets.Add(absolute);
            offset = absolute + 1;
        }

        return offsets;
    }

    private static int FindAsciiText(byte[] buffer, byte[] pattern, int startOffset, bool forward)
    {
        if (pattern.Length == 0 || pattern.Length > buffer.Length)
        {
            return -1;
        }

        startOffset = Math.Clamp(startOffset, 0, buffer.Length - 1);
        if (forward)
        {
            for (var offset = startOffset; offset <= buffer.Length - pattern.Length; offset++)
            {
                if (AsciiEqualsIgnoreCase(buffer, pattern, offset))
                {
                    return offset;
                }
            }

            return -1;
        }

        for (var offset = Math.Min(startOffset, buffer.Length - pattern.Length); offset >= 0; offset--)
        {
            if (AsciiEqualsIgnoreCase(buffer, pattern, offset))
            {
                return offset;
            }
        }

        return -1;
    }

    private static List<int> FindAllAsciiText(byte[] buffer, byte[] pattern)
    {
        var offsets = new List<int>();
        if (pattern.Length == 0 || pattern.Length > buffer.Length)
        {
            return offsets;
        }

        for (var offset = 0; offset <= buffer.Length - pattern.Length; offset++)
        {
            if (AsciiEqualsIgnoreCase(buffer, pattern, offset))
            {
                offsets.Add(offset);
            }
        }

        return offsets;
    }

    private static bool AsciiEqualsIgnoreCase(byte[] buffer, byte[] pattern, int offset)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            if (ToAsciiUpper(buffer[offset + i]) != ToAsciiUpper(pattern[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte ToAsciiUpper(byte value) => value is >= (byte)'a' and <= (byte)'z'
        ? (byte)(value - 32)
        : value;

    private static bool TryParseHexPattern(string text, out byte[] pattern)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (Uri.IsHexDigit(ch))
            {
                if (ch == '0' && i + 1 < text.Length && text[i + 1] is 'x' or 'X')
                {
                    i++;
                    continue;
                }

                builder.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '_' or ',' or ';')
            {
                continue;
            }

            pattern = [];
            return false;
        }

        var hex = builder.ToString();
        if (hex.Length == 0 || hex.Length % 2 != 0)
        {
            pattern = [];
            return false;
        }

        pattern = new byte[hex.Length / 2];
        for (var i = 0; i < pattern.Length; i++)
        {
            if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out pattern[i]))
            {
                pattern = [];
                return false;
            }
        }

        return true;
    }

    private static string FormatHexPattern(byte[] pattern) =>
        string.Join(" ", pattern.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));

    private static bool TryParseOffset(string text, out int offset)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out offset);
    }

    private void ThemeToggleButton_Toggled(object sender, RoutedEventArgs e)
    {
        ApplyTheme(ThemeToggleButton.IsChecked == true);
    }

    private void ApplyTheme(bool darkMode)
    {
        ThemeIconPath.Data = Geometry.Parse(darkMode ? SunIconPath : MoonIconPath);
        ThemeToggleButton.ToolTip = darkMode ? "Switch to light mode" : "Switch to dark mode";

        if (darkMode)
        {
            SetBrush("AppBackgroundBrush", "#202225");
            SetBrush("PanelBackgroundBrush", "#25282C");
            SetBrush("ToolbarBackgroundBrush", "#2D3035");
            SetBrush("ToolbarButtonBackgroundBrush", "#2D3035");
            SetBrush("ToolbarButtonBorderBrush", "#2D3035");
            SetBrush("SurfaceBackgroundBrush", "#1F2226");
            SetBrush("SubtleBackgroundBrush", "#272B30");
            SetBrush("InputBackgroundBrush", "#2C3137");
            SetBrush("ReadOnlyBackgroundBrush", "#293036");
            SetBrush("HoverBackgroundBrush", "#353C44");
            SetBrush("PressedBackgroundBrush", "#3D4650");
            SetBrush("AccentBrush", "#69BDFD");
            SetBrush("AccentSoftBrush", "#19384F");
            SetBrush("AlternateRowBackgroundBrush", "#24282D");
            SetBrush("TextBrush", "#E7ECF2");
            SetBrush("MutedTextBrush", "#B8C1CC");
            SetBrush("BorderBrush", "#48515C");
            SetBrush("GridLineBrush", "#313740");
            SetBrush("LightGridLineBrush", "#2A3038");
            SetBrush("AddressBackgroundBrush", "#283A49");
            SetBrush("AddressForegroundBrush", "#6BCBFF");
            SetBrush("SplitterBrush", "#3A424C");
            SetBrush("SelectionBackgroundBrush", "#315B7C");
            SetBrush("SelectionForegroundBrush", "#FFFFFF");
            SetBrush("ProgressTrackBrush", "#30363D");
            SetBrush("StopBackgroundBrush", "#4A2328");
            SetBrush("StopForegroundBrush", "#FFB9C0");
            return;
        }

        SetBrush("AppBackgroundBrush", "#F0F0F0");
        SetBrush("PanelBackgroundBrush", "#EFEFEF");
        SetBrush("ToolbarBackgroundBrush", "#E8E8E8");
        SetBrush("ToolbarButtonBackgroundBrush", "#E8E8E8");
        SetBrush("ToolbarButtonBorderBrush", "#E8E8E8");
        SetBrush("SurfaceBackgroundBrush", "#FFFFFF");
        SetBrush("SubtleBackgroundBrush", "#F6F6F6");
        SetBrush("InputBackgroundBrush", "#F7F7F7");
        SetBrush("ReadOnlyBackgroundBrush", "#F7F7F7");
        SetBrush("HoverBackgroundBrush", "#E9F3FF");
        SetBrush("PressedBackgroundBrush", "#DDEEFF");
        SetBrush("AccentBrush", "#0067C0");
        SetBrush("AccentSoftBrush", "#E5F1FB");
        SetBrush("AlternateRowBackgroundBrush", "#FBFBFB");
        SetBrush("TextBrush", "#000000");
        SetBrush("MutedTextBrush", "#333333");
        SetBrush("BorderBrush", "#B8B8B8");
        SetBrush("GridLineBrush", "#E6E6E6");
        SetBrush("LightGridLineBrush", "#F6F6F6");
        SetBrush("AddressBackgroundBrush", "#A8A8A8");
        SetBrush("AddressForegroundBrush", "#FFFFFF");
        SetBrush("SplitterBrush", "#D8D8D8");
        SetBrush("SelectionBackgroundBrush", "#DDEEFF");
        SetBrush("SelectionForegroundBrush", "#000000");
        SetBrush("ProgressTrackBrush", "#E5E5E5");
        SetBrush("StopBackgroundBrush", "#FFF0F0");
        SetBrush("StopForegroundBrush", "#B00000");
    }

    private static void SetBrush(string key, string color)
    {
        Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private void LoadFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Binary files (*.bin;*.rom)|*.bin;*.rom|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _buffer = File.ReadAllBytes(dialog.FileName);
            _currentOffset = 0;
            _searchHits.Clear();
            RebuildRows();
            UpdateStatus();
            AppendLog($"Loaded {dialog.FileName} ({FormatBytes(_buffer.Length)})");
        }
        catch (Exception ex)
        {
            AppendLog($"Open file failed: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Open file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Binary files (*.bin)|*.bin|ROM files (*.rom)|*.rom|All files (*.*)|*.*",
            FileName = $"{CurrentChip().Name}.bin"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllBytes(dialog.FileName, _buffer);
        AppendLog($"Saved {dialog.FileName} ({FormatBytes(_buffer.Length)})");
    }

    private void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LogBox.Text))
        {
            MessageBox.Show(this, "Log is empty", AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"Multi-Flash-Log-{DateTime.Now:yyyyMMdd-HHmmss}.log"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, LogBox.Text, Encoding.UTF8);
        AppendLog($"Log saved: {dialog.FileName}");
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
    }

    private void FillFF_Click(object sender, RoutedEventArgs e)
    {
        Array.Fill(_buffer, (byte)0xFF);
        RebuildRows();
        AppendLog("Buffer filled with FF");
    }

    private void Fill00_Click(object sender, RoutedEventArgs e)
    {
        Array.Fill(_buffer, (byte)0x00);
        RebuildRows();
        AppendLog("Buffer filled with 00");
    }

    private async void RunScript_Click(object sender, RoutedEventArgs e)
    {
        var script = (sender as FrameworkElement)?.Tag as string ?? "Script";
        if (!EnsureProgrammerAvailable(script))
        {
            return;
        }

        await RunOperationAsync(script, _buffer.Length, async progress =>
        {
            var chip = CurrentChip();
            var startAddress = ParseStartAddress();
            if (string.Equals(script, "Read + verify", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog($"Script request: read and verify {FormatBytes(_buffer.Length)} from 0x{startAddress:X6}");
                AppendLog("Script stage: read started");
                _buffer = await _programmer.ReadAsync(chip, startAddress, _buffer.Length, progress);
                RebuildRows();
                UpdateStatus();
                AppendLog("Script stage: read completed");
                AppendLog("Script stage: verify started");
                var readOk = await _programmer.VerifyAsync(chip, startAddress, _buffer, progress);
                AppendLog(readOk ? "Script stage: verify completed OK" : "Script stage: verify failed");
                AppendLog(readOk ? "Script completed: read + verify OK" : "Script completed: read + verify failed");
                return;
            }

            AppendLog($"Script request: erase, write and verify {FormatBytes(_buffer.Length)} at 0x{startAddress:X6}");
            await UnprotectIfRequestedAsync(chip, progress);
            AppendLog("Script stage: erase started");
            await _programmer.EraseAsync(chip, progress);
            AppendLog("Script stage: erase completed");
            AppendLog("Script stage: write started");
            await _programmer.WriteAsync(chip, startAddress, _buffer, progress, skipBlankPages: true);
            AppendLog("Script stage: write completed");
            AppendLog("Script stage: verify started");
            var ok = await _programmer.VerifyAsync(chip, startAddress, _buffer, progress);
            AppendLog(ok ? "Script stage: verify completed OK" : "Script stage: verify failed");
            AppendLog(ok ? "Script completed: verify OK" : "Script completed: verify failed");
        });
    }

    private async Task UnprotectIfRequestedAsync(ChipProfile chip, IProgress<int> progress)
    {
        if (UnprotectChipCheckBox.IsChecked != true)
        {
            return;
        }

        AppendLog($"Unprotect request: {chip.Name}");
        await _programmer.UnprotectAsync(chip, progress);
        AppendLog("Unprotect completed");
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(ProjectUrl)
        {
            UseShellExecute = true
        });
    }

    private static string AppVersion =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    private Task RunOperationAsync(string name, Func<IProgress<int>, Task> operation) =>
        RunOperationAsync(name, null, operation);

    private Task RunOperationAsync(string name, Func<IProgress<int>, Task> operation, bool logLifecycle) =>
        RunOperationAsync(name, null, operation, logLifecycle);

    private async Task RunOperationAsync(string name, int? byteCount, Func<IProgress<int>, Task> operation, bool logLifecycle = true)
    {
        if (_isBusy)
        {
            AppendLog("Another operation is already running");
            return;
        }

        _isBusy = true;
        OperationStatusText.Text = name;
        OperationProgress.Value = 0;
        var progress = new Progress<int>(value => OperationProgress.Value = Math.Clamp(value, 0, 100));
        var stopwatch = Stopwatch.StartNew();
        if (logLifecycle)
        {
            AppendLog($"{name} started");
        }

        try
        {
            await operation(progress);
            stopwatch.Stop();
            OperationProgress.Value = 100;
            OperationStatusText.Text = "Ready";
            if (logLifecycle)
            {
                AppendLog(byteCount is > 0
                    ? $"{name} completed: {FormatBytes(byteCount.Value)} in {FormatDuration(stopwatch.Elapsed)} ({FormatSpeed(byteCount.Value, stopwatch.Elapsed)})"
                    : $"{name} completed in {FormatDuration(stopwatch.Elapsed)}");
            }

            PlayOperationSound(name, success: true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            OperationStatusText.Text = "Error";
            AppendLog($"ERROR after {FormatDuration(stopwatch.Elapsed)}: {ex.Message}");
            PlayOperationSound(name, success: false);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private static void PlayOperationSound(string operationName, bool success)
    {
        if (!ShouldPlayCompletionSound(operationName))
        {
            return;
        }

        try
        {
            (success ? SystemSounds.Asterisk : SystemSounds.Hand).Play();
        }
        catch
        {
            // Best-effort only; sound failures must never affect programmer operations.
        }
    }

    private static bool ShouldPlayCompletionSound(string operationName) =>
        operationName.StartsWith("Read chip", StringComparison.OrdinalIgnoreCase) ||
        operationName.StartsWith("Write chip", StringComparison.OrdinalIgnoreCase) ||
        operationName.StartsWith("Erase chip", StringComparison.OrdinalIgnoreCase) ||
        operationName.StartsWith("Verify", StringComparison.OrdinalIgnoreCase) ||
        operationName.Contains("verify", StringComparison.OrdinalIgnoreCase);

    private ChipProfile CurrentChip() => ChipCombo.SelectedItem as ChipProfile ?? _chips[0];

    private void ShowChipSelectionForId(byte[] id, bool autoApplySingle = false, bool openCatalogOnMiss = true)
    {
        if (id.Length == 0)
        {
            AppendLog("IC ID is empty; skipped IC auto-detect");
            return;
        }

        var idText = FormatId(id);
        var candidates = FindCandidatesByJedecId(id).ToList();
        if (candidates.Count == 0 && TryDetectJedecChip(id, out var fallback))
        {
            candidates.Add(new IcCandidate(fallback.Name, fallback.Volts, FormatMbits(fallback.SizeBytes), $"{fallback.PageSize} Bytes", fallback.Manufacturer, fallback.Type, fallback, idText));
        }

        if (candidates.Count == 0)
        {
            if (!openCatalogOnMiss)
            {
                AppendLog("IC ID is not in the detection table");
                return;
            }

            AppendLog("IC ID is not in the detection table. Opening full IC list");
            ShowChipSelection(_icCatalog, "Search IC", null);
            return;
        }

        AppendLog(candidates.Count == 1
            ? $"Detected ID: {idText}. One compatible IC profile found"
            : $"Detected ID: {idText}. Multiple compatible IC profiles found. Please select the exact chip marking");

        if (autoApplySingle && candidates.Count == 1)
        {
            var candidate = candidates[0];
            ApplyChip(candidate.Profile);
            AppendLog($"Auto-selected IC: {candidate.Device}, {candidate.Size}, page {candidate.Page}, {candidate.Manuf}");
            return;
        }

        ShowChipSelection(candidates, "Search IC", idText);
    }

    private void ShowChipSelection(IEnumerable<IcCandidate> candidates, string title, string? idText)
    {
        var dialog = new SearchIcWindow(candidates, idText)
        {
            Owner = this,
            Title = title
        };

        if (dialog.ShowDialog() == true && dialog.SelectedCandidate is not null)
        {
            ApplyChip(dialog.SelectedCandidate.Profile);
            AppendLog($"Selected IC: {dialog.SelectedCandidate.Device}, {dialog.SelectedCandidate.Size}, page {dialog.SelectedCandidate.Page}, {dialog.SelectedCandidate.Manuf}");
        }
    }

    private void ApplyChip(ChipProfile chip)
    {
        _isApplyingDetectedChip = true;
        try
        {
            var knownChip = _chips.FirstOrDefault(x => x.Name == chip.Name) ?? chip;
            if (!_chips.Any(x => x.Name == knownChip.Name))
            {
                _chips.Add(knownChip);
            }

            ChipCombo.SelectedItem = knownChip;
            SelectSize(knownChip.SizeBytes);
            PageCombo.SelectedItem = knownChip.PageSize.ToString();
            CommandCombo.SelectedItem = knownChip.CommandSet;
            ResizeBuffer(knownChip.SizeBytes, fill: 0xFF);
            UpdateDeviceInfo(knownChip);
        }
        finally
        {
            _isApplyingDetectedChip = false;
        }
    }

    private void UpdateDeviceInfo(ChipProfile chip)
    {
        DeviceNameBox.Text = chip.Name;
        DeviceTypeText.Text = chip.Type;
        BitSizeText.Text = FormatMbits(chip.SizeBytes);
        ManufacturerText.Text = chip.Manufacturer;
        DeviceSizeBox.Text = chip.SizeBytes.ToString();
    }

    private bool TryDetectJedecChip(byte[] id, out ChipProfile chip)
    {
        chip = CurrentChip();
        if (id.Length < 3)
        {
            return false;
        }

        var detectedName = (id[0], id[1], id[2]) switch
        {
            (0xEF, 0x40, 0x14) => "W25Q80",
            (0xEF, 0x40, 0x15) => "W25Q16",
            (0xEF, 0x40, 0x16) => "W25Q32",
            (0xEF, 0x40, 0x17) => "W25Q64",
            (0xEF, 0x40, 0x18) => "W25Q128",
            (0xC2, 0x20, 0x15) => "MX25L1606E",
            (0xC2, 0x20, 0x16) => "MX25L3206E",
            (0xC2, 0x20, 0x17) => "MX25L6406E",
            (0xC2, 0x20, 0x18) => "MX25L12835F",
            (0xC8, 0x40, 0x15) => "GD25Q16",
            (0xC8, 0x40, 0x16) => "GD25Q32",
            (0xC8, 0x40, 0x17) => "GD25Q64",
            (0xC8, 0x40, 0x18) => "GD25Q128",
            _ => null
        };

        if (detectedName is null)
        {
            return false;
        }

        var detectedChip = _chips.FirstOrDefault(x => x.Name == detectedName);
        if (detectedChip is null)
        {
            detectedChip = detectedName switch
            {
                "W25Q80" => new ChipProfile("W25Q80", "SPI", 1024 * 1024, 256, "25xx", "WINBOND", "3.3V", "SPI_NOR"),
                _ => null
            };
        }

        if (detectedChip is null)
        {
            return false;
        }

        chip = detectedChip;
        return true;
    }

    private IEnumerable<IcCandidate> FindCandidatesByJedecId(byte[] id)
    {
        var idText = FormatId(id);
        return _icCatalog.Where(x => string.Equals(x.JedecId, idText, StringComparison.OrdinalIgnoreCase));
    }

    private List<IcCandidate> BuildIcCatalog()
    {
        var list = new List<IcCandidate>();
        AddWinbondFamily(list, "EF 40 14", "W25Q80", 1024 * 1024, "8 Mbits");
        AddWinbondFamily(list, "EF 40 15", "W25Q16", 2 * 1024 * 1024, "16 Mbits");
        AddWinbondFamily(list, "EF 40 16", "W25Q32", 4 * 1024 * 1024, "32 Mbits");
        AddWinbondFamily(list, "EF 40 17", "W25Q64", 8 * 1024 * 1024, "64 Mbits");
        AddWinbondFamily(list, "EF 40 18", "W25Q128", 16 * 1024 * 1024, "128 Mbits");

        AddCandidate(list, "MX25L1606E", "3.3V", "16 Mbits", "256 Bytes", "MACRONIX", "SPI_NOR", "C2 20 15", 2 * 1024 * 1024);
        AddCandidate(list, "MX25L3206E", "3.3V", "32 Mbits", "256 Bytes", "MACRONIX", "SPI_NOR", "C2 20 16", 4 * 1024 * 1024);
        AddCandidate(list, "MX25L6406E", "3.3V", "64 Mbits", "256 Bytes", "MACRONIX", "SPI_NOR", "C2 20 17", 8 * 1024 * 1024);
        AddCandidate(list, "MX25L12835F", "3.3V", "128 Mbits", "256 Bytes", "MACRONIX", "SPI_NOR", "C2 20 18", 16 * 1024 * 1024);
        AddCandidate(list, "GD25Q16", "3.3V", "16 Mbits", "256 Bytes", "GIGADEVICE", "SPI_NOR", "C8 40 15", 2 * 1024 * 1024);
        AddCandidate(list, "GD25Q32", "3.3V", "32 Mbits", "256 Bytes", "GIGADEVICE", "SPI_NOR", "C8 40 16", 4 * 1024 * 1024);
        AddCandidate(list, "GD25Q64", "3.3V", "64 Mbits", "256 Bytes", "GIGADEVICE", "SPI_NOR", "C8 40 17", 8 * 1024 * 1024);
        AddCandidate(list, "GD25Q128", "3.3V", "128 Mbits", "256 Bytes", "GIGADEVICE", "SPI_NOR", "C8 40 18", 16 * 1024 * 1024);

        AddCandidate(list, "24C02", "5V/3.3V", "2 Kbits", "8 Bytes", "GENERIC", "I2C_EEPROM", "", 256, "I2C", 8, "24xx");
        AddCandidate(list, "24C04", "5V/3.3V", "4 Kbits", "16 Bytes", "GENERIC", "I2C_EEPROM", "", 512, "I2C", 16, "24xx");
        AddCandidate(list, "24C08", "5V/3.3V", "8 Kbits", "16 Bytes", "GENERIC", "I2C_EEPROM", "", 1024, "I2C", 16, "24xx");
        AddCandidate(list, "24C16", "5V/3.3V", "16 Kbits", "16 Bytes", "GENERIC", "I2C_EEPROM", "", 2048, "I2C", 16, "24xx");
        AddCandidate(list, "24C32", "5V/3.3V", "32 Kbits", "32 Bytes", "GENERIC", "I2C_EEPROM", "", 4096, "I2C", 32, "24xx");
        AddCandidate(list, "24C64", "5V/3.3V", "64 Kbits", "32 Bytes", "GENERIC", "I2C_EEPROM", "", 8192, "I2C", 32, "24xx");
        AddCandidate(list, "24C128", "5V/3.3V", "128 Kbits", "64 Bytes", "GENERIC", "I2C_EEPROM", "", 16384, "I2C", 64, "24xx");
        AddCandidate(list, "24C256", "5V/3.3V", "256 Kbits", "64 Bytes", "GENERIC", "I2C_EEPROM", "", 32768, "I2C", 64, "24xx");
        AddCandidate(list, "24C512", "5V/3.3V", "512 Kbits", "128 Bytes", "GENERIC", "I2C_EEPROM", "", 65536, "I2C", 128, "24xx");
        AddCandidate(list, "93C46", "5V/3.3V", "1 Kbits", "16 Bytes", "GENERIC", "MICROWIRE (CATALOG_ONLY)", "", 128, "Microwire", 16, "93xx");
        AddCandidate(list, "93C86", "5V/3.3V", "16 Kbits", "16 Bytes", "GENERIC", "MICROWIRE (CATALOG_ONLY)", "", 2048, "Microwire", 16, "93xx");
        AddIntegratedIcCatalog(list);
        AddT48IcCatalog(list);
        return list;
    }

    private static void AddIntegratedIcCatalog(List<IcCandidate> list)
    {
        AddTsvIcCatalog(list, "IntegratedICCatalog.tsv");
    }

    private static void AddT48IcCatalog(List<IcCandidate> list)
    {
        AddTsvIcCatalog(list, "T48ICCatalog.tsv");
    }

    private static void AddTsvIcCatalog(List<IcCandidate> list, string fileName)
    {
        var catalogPath = FindCatalogFile(fileName);
        if (catalogPath is null)
        {
            return;
        }

        var knownDevices = list.Select(x => x.Device).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(catalogPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith("Device\t"))
            {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length < 10 ||
                !int.TryParse(fields[3], out var sizeBytes) ||
                !int.TryParse(fields[4], out var pageSize) ||
                sizeBytes <= 0 ||
                pageSize <= 0 ||
                !knownDevices.Add(fields[0]))
            {
                continue;
            }

            AddCandidate(
                list,
                fields[0],
                FormatVolts(fields[5]),
                FormatMbits(sizeBytes),
                $"{pageSize} Bytes",
                string.IsNullOrWhiteSpace(fields[1]) ? "GENERIC" : fields[1].ToUpperInvariant(),
                fields[8],
                FormatRawId(fields[2]),
                sizeBytes,
                fields[6],
                pageSize,
                fields[7]);
        }
    }

    private static string? FindCatalogFile(string fileName)
    {
        var catalogPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(catalogPath))
        {
            return catalogPath;
        }

        catalogPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", fileName);
        return File.Exists(catalogPath) ? catalogPath : null;
    }

    private static void AddWinbondFamily(List<IcCandidate> list, string id, string baseName, int bytes, string bitSize)
    {
        var variants = baseName == "W25Q128"
            ? new[] { "W25M121AV", "W25Q128BV", "W25Q128FV", "W25Q128JV", "W25Q128JVSC", "W25Q128JVFQ", "W25Q128JVPQ", "W25Q128JVEC", "W25Q128JVBQ", "W25Q128JVCQ" }
            : new[] { baseName, $"{baseName}FV", $"{baseName}JV" };

        foreach (var variant in variants)
        {
            AddCandidate(list, variant, "3.3V", bitSize, "256 Bytes", "WINBOND", variant == "W25M121AV" ? "SPI_STACK" : "SPI_NOR", id, bytes);
        }
    }

    private static void AddCandidate(List<IcCandidate> list, string device, string volts, string size, string page, string manuf, string type, string jedecId, int bytes, string protocol = "SPI", int pageSize = 256, string commandSet = "25xx")
    {
        var profile = new ChipProfile(device, protocol, bytes, pageSize, commandSet, manuf, volts, type);
        list.Add(new IcCandidate(device, volts, size, page, manuf, type, profile, jedecId));
    }

    private static string FormatRawId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        var hex = new string(id.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return string.Join(" ", Enumerable.Range(0, hex.Length / 2).Select(i => hex.Substring(i * 2, 2)));
    }

    private static string FormatVolts(string? volts) =>
        string.IsNullOrWhiteSpace(volts) ? string.Empty : volts.EndsWith('V') ? volts : $"{volts}V";

    private static string FormatId(byte[] id) => string.Join(" ", id.Select(x => x.ToString("X2")));

    private static string FormatMbits(int bytes)
    {
        var bits = bytes * 8.0;
        return bits >= 1024 * 1024
            ? $"{bits / (1024 * 1024):0.#} Mbits"
            : $"{bits / 1024:0.#} Kbits";
    }

    private int ParseStartAddress()
    {
        var text = StartAddressBox.Text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value) ? value : 0;
    }

    private void UpdateStatus()
    {
        SizeStatusText.Text = $"Size: {_buffer.Length}";
        BufferStatusText.Text = $"Buffer: {FormatBytes(_buffer.Length)}";
    }

    private void AppendLog(string message)
    {
        message = message.TrimEnd('.');
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private static string FormatBytes(int bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):0.##} MB";
        }

        return bytes >= 1024 ? $"{bytes / 1024.0:0.##} KB" : $"{bytes} B";
    }

    private static string FormatDuration(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss\.fff")
            : elapsed.ToString(@"mm\:ss\.fff");
    }

    private static string FormatSpeed(int bytes, TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds <= 0)
        {
            return "n/a";
        }

        var bytesPerSecond = bytes / elapsed.TotalSeconds;
        return bytesPerSecond >= 1024 * 1024
            ? $"{bytesPerSecond / (1024 * 1024):0.##} MB/s"
            : $"{bytesPerSecond / 1024:0.##} KB/s";
    }
}

public sealed record ChipProfile(
    string Name,
    string Protocol,
    int SizeBytes,
    int PageSize,
    string CommandSet,
    string Manufacturer = "GENERIC",
    string Volts = "",
    string Type = "");

public sealed record IcCandidate(
    string Device,
    string Volts,
    string Size,
    string Page,
    string Manuf,
    string Type,
    ChipProfile Profile,
    string JedecId);

public sealed record SizeOption(string Label, int Bytes);

public sealed record SearchResult(bool Found, int Offset, int Length, string Message)
{
    public static SearchResult Success(int offset, int length) => new(true, offset, length, string.Empty);

    public static SearchResult Fail(string message) => new(false, -1, 0, message);
}

public sealed record SearchHit(int Offset, int Length, string OffsetText, string HexExcerpt, string TextExcerpt)
{
    public static SearchHit Message(string message) => new(-1, 0, string.Empty, message, string.Empty);
}

public sealed record WindowsKeyCandidate(string Method, int Offset, string Key, int Length, string Description);

public sealed class HexRow : INotifyPropertyChanged
{
    private readonly byte[] _buffer;
    private readonly int _offset;
    private readonly Action<int, byte> _onChanged;

    public HexRow(byte[] buffer, int offset, Action<int, byte> onChanged)
    {
        _buffer = buffer;
        _offset = offset;
        _onChanged = onChanged;
    }

    public string Address => $"0x{_offset:X8}";
    public string B0 { get => Get(0); set => Set(0, value); }
    public string B1 { get => Get(1); set => Set(1, value); }
    public string B2 { get => Get(2); set => Set(2, value); }
    public string B3 { get => Get(3); set => Set(3, value); }
    public string B4 { get => Get(4); set => Set(4, value); }
    public string B5 { get => Get(5); set => Set(5, value); }
    public string B6 { get => Get(6); set => Set(6, value); }
    public string B7 { get => Get(7); set => Set(7, value); }
    public string B8 { get => Get(8); set => Set(8, value); }
    public string B9 { get => Get(9); set => Set(9, value); }
    public string BA { get => Get(10); set => Set(10, value); }
    public string BB { get => Get(11); set => Set(11, value); }
    public string BC { get => Get(12); set => Set(12, value); }
    public string BD { get => Get(13); set => Set(13, value); }
    public string BE { get => Get(14); set => Set(14, value); }
    public string BF { get => Get(15); set => Set(15, value); }

    public string Ascii
    {
        get
        {
            var builder = new StringBuilder(16);
            for (var i = 0; i < 16; i++)
            {
                var value = ReadByte(i);
                builder.Append(value is >= 32 and <= 126 ? (char)value : '.');
            }

            return builder.ToString();
        }
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            for (var i = 0; i < 16 && i < value.Length && _offset + i < _buffer.Length; i++)
            {
                var next = value[i] is >= ' ' and <= '~' ? (byte)value[i] : (byte)'.';
                _onChanged(_offset + i, next);
                OnPropertyChanged(CellName(i));
            }

            RefreshAscii();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshAscii() => OnPropertyChanged(nameof(Ascii));

    private string Get(int index) => _offset + index < _buffer.Length ? _buffer[_offset + index].ToString("X2") : string.Empty;

    private void Set(int index, string value)
    {
        if (_offset + index >= _buffer.Length)
        {
            return;
        }

        value = value.Trim();
        if (value.Length > 2)
        {
            value = value[^2..];
        }

        if (byte.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
        {
            _onChanged(_offset + index, parsed);
            OnPropertyChanged(CellName(index));
            RefreshAscii();
        }
    }

    private byte ReadByte(int index) => _offset + index < _buffer.Length ? _buffer[_offset + index] : (byte)0;

    private static string CellName(int index) => index switch
    {
        0 => nameof(B0),
        1 => nameof(B1),
        2 => nameof(B2),
        3 => nameof(B3),
        4 => nameof(B4),
        5 => nameof(B5),
        6 => nameof(B6),
        7 => nameof(B7),
        8 => nameof(B8),
        9 => nameof(B9),
        10 => nameof(BA),
        11 => nameof(BB),
        12 => nameof(BC),
        13 => nameof(BD),
        14 => nameof(BE),
        _ => nameof(BF)
    };

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public interface IChipProgrammer
{
    string Name { get; }
    Task<bool> DetectAsync(IProgress<int> progress);
    Task<byte[]> ReadIdAsync(ChipProfile chip, IProgress<int> progress);
    Task<byte[]> ReadAsync(ChipProfile chip, int startAddress, int length, IProgress<int> progress);
    Task WriteAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress, bool skipBlankPages = false);
    Task<bool> VerifyAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress);
    Task UnprotectAsync(ChipProfile chip, IProgress<int> progress);
    Task EraseAsync(ChipProfile chip, IProgress<int> progress);
}

public sealed class Ch347NativeProgrammer : IChipProgrammer
{
    private const int DeviceIndex = 0;
    private const uint ChipSelect = 0x80;
    private const int ReadChunkSize = 32768;
    private const int I2cReadChunkSize = 512;
    private const int PageProgramDelayMs = 1;

    public string Name => "CH347 native DLL";

    public static bool IsAvailable =>
        File.Exists(Path.Combine(Environment.SystemDirectory, "CH347DLLA64.DLL")) ||
        File.Exists(Path.Combine(AppContext.BaseDirectory, "CH347DLLA64.DLL"));

    public static bool CanOpenDevice()
    {
        var handle = NativeMethods.CH347OpenDevice(DeviceIndex);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        NativeMethods.CH347CloseDevice(DeviceIndex);
        return true;
    }

    public async Task<bool> DetectAsync(IProgress<int> progress)
    {
        progress.Report(10);
        await Task.Yield();
        var handle = NativeMethods.CH347OpenDevice(DeviceIndex);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            progress.Report(100);
            return false;
        }

        NativeMethods.CH347CloseDevice(DeviceIndex);
        progress.Report(100);
        return true;
    }

    public Task<byte[]> ReadIdAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(() =>
    {
        if (IsI2c(chip))
        {
            progress.Report(100);
            return Array.Empty<byte>();
        }

        EnsureSpi(chip);
        using var device = OpenDevice();
        progress.Report(25);
        var id = SpiTransfer([0x9F, 0x00, 0x00, 0x00]);
        progress.Report(100);
        return id.Skip(1).Take(3).ToArray();
    });

    public Task<byte[]> ReadAsync(ChipProfile chip, int startAddress, int length, IProgress<int> progress) => Task.Run(() =>
    {
        if (IsI2c(chip))
        {
            using var device = OpenDevice();
            return ReadI2cEeprom(chip, startAddress, length, progress);
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        var result = new byte[length];
        var done = 0;

        while (done < length)
        {
            var count = Math.Min(ReadChunkSize, length - done);
            var address = startAddress + done;
            var addressBytes = Uses4ByteAddress(chip, address) ? 4 : 3;
            var command = new byte[count + 1 + addressBytes];
            WriteAddress(command, 0, 0x03, 0x13, address, addressBytes);
            var response = SpiTransfer(command);
            Buffer.BlockCopy(response, command.Length - count, result, done, count);
            done += count;
            progress.Report(length == 0 ? 100 : done * 100 / length);
        }

        progress.Report(100);
        return result;
    });

    public Task WriteAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress, bool skipBlankPages = false) => Task.Run(async () =>
    {
        if (IsI2c(chip))
        {
            using var device = OpenDevice();
            await WriteI2cEepromAsync(chip, startAddress, data, progress, skipBlankPages);
            return;
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        var done = 0;

        while (done < data.Length)
        {
            var pageOffset = (startAddress + done) % chip.PageSize;
            var count = Math.Min(chip.PageSize - pageOffset, data.Length - done);
            if (skipBlankPages && IsBlank(data, done, count))
            {
                done += count;
                progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
                continue;
            }

            WriteEnable();

            var address = startAddress + done;
            var addressBytes = Uses4ByteAddress(chip, address) ? 4 : 3;
            var headerLength = 1 + addressBytes;
            var command = new byte[count + headerLength];
            WriteAddress(command, 0, 0x02, 0x12, address, addressBytes);
            Buffer.BlockCopy(data, done, command, headerLength, count);
            SpiTransfer(command);
            await WaitUntilReadyAsync();

            done += count;
            progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
        }

        progress.Report(100);
    });

    public async Task<bool> VerifyAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress)
    {
        var actual = await ReadAsync(chip, startAddress, data.Length, progress);
        return actual.SequenceEqual(data);
    }

    public Task UnprotectAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(async () =>
    {
        if (IsI2c(chip))
        {
            throw new NotSupportedException("I2C EEPROM does not use SPI NOR block-protect status bits.");
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        await ClearSpiNorProtectionAsync(progress);
    });

    public Task EraseAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(async () =>
    {
        if (IsI2c(chip))
        {
            using var device = OpenDevice();
            var blank = Enumerable.Repeat((byte)0xFF, chip.SizeBytes).ToArray();
            await WriteI2cEepromAsync(chip, 0, blank, progress, skipBlankPages: false);
            return;
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        WriteEnable();
        SpiTransfer([0xC7]);
        progress.Report(5);

        for (var i = 0; i < 600; i++)
        {
            if (!IsBusy())
            {
                progress.Report(100);
                return;
            }

            progress.Report(Math.Min(95, 5 + i / 7));
            await Task.Delay(100);
        }

        throw new TimeoutException("Erase timeout. Chip still reports WIP=1.");
    });

    private static Ch347Device OpenDevice()
    {
        var handle = NativeMethods.CH347OpenDevice(DeviceIndex);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            throw new InvalidOperationException("Cannot open CH347. Check USB connection, WCH CH347 driver, and that no other programmer software is using it.");
        }

        return new Ch347Device();
    }

    private static byte[] SpiTransfer(byte[] buffer)
    {
        var io = buffer.ToArray();
        if (!NativeMethods.CH347StreamSPI4(DeviceIndex, ChipSelect, (uint)io.Length, io))
        {
            if (!NativeMethods.CH347StreamSPI4(DeviceIndex, 0, (uint)io.Length, io))
            {
                throw new IOException("CH347 SPI transfer failed.");
            }
        }

        return io;
    }

    private static byte[] ReadI2cEeprom(ChipProfile chip, int startAddress, int length, IProgress<int> progress)
    {
        var result = new byte[length];
        var done = 0;
        while (done < length)
        {
            var count = Math.Min(I2cReadChunkSize, length - done);
            var address = startAddress + done;
            var write = BuildI2cAddressWriteBuffer(chip, address);
            var read = new byte[count];

            if (!NativeMethods.CH347StreamI2C(DeviceIndex, (uint)write.Length, write, (uint)read.Length, read))
            {
                throw new IOException($"CH347 I2C read failed at 0x{address:X6}.");
            }

            Buffer.BlockCopy(read, 0, result, done, count);
            done += count;
            progress.Report(length == 0 ? 100 : done * 100 / length);
        }

        progress.Report(100);
        return result;
    }

    private static async Task WriteI2cEepromAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress, bool skipBlankPages)
    {
        var done = 0;
        while (done < data.Length)
        {
            var address = startAddress + done;
            var pageOffset = address % chip.PageSize;
            var count = Math.Min(chip.PageSize - pageOffset, data.Length - done);
            if (skipBlankPages && IsBlank(data, done, count))
            {
                done += count;
                progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
                continue;
            }

            var write = BuildI2cPageWriteBuffer(chip, address, data, done, count);

            if (!NativeMethods.CH347StreamI2C(DeviceIndex, (uint)write.Length, write, 0, Array.Empty<byte>()))
            {
                throw new IOException($"CH347 I2C write failed at 0x{address:X6}.");
            }

            done += count;
            progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
            await Task.Delay(6);
        }

        progress.Report(100);
    }

    private static byte[] BuildI2cAddressWriteBuffer(ChipProfile chip, int address)
    {
        var device = I2cDeviceWriteAddress(chip, address);
        if (UsesOneByteI2cAddress(chip))
        {
            return [(byte)device, (byte)(address & 0xFF)];
        }

        return [(byte)device, (byte)((address >> 8) & 0xFF), (byte)(address & 0xFF)];
    }

    private static byte[] BuildI2cPageWriteBuffer(ChipProfile chip, int address, byte[] data, int dataOffset, int count)
    {
        var prefix = BuildI2cAddressWriteBuffer(chip, address);
        var write = new byte[prefix.Length + count];
        Buffer.BlockCopy(prefix, 0, write, 0, prefix.Length);
        Buffer.BlockCopy(data, dataOffset, write, prefix.Length, count);
        return write;
    }

    private static int I2cDeviceWriteAddress(ChipProfile chip, int address)
    {
        var block = UsesOneByteI2cAddress(chip) ? (address >> 8) & 0x07 : 0;
        return 0xA0 | (block << 1);
    }

    private static bool UsesOneByteI2cAddress(ChipProfile chip) => chip.SizeBytes <= 2048;

    private static bool IsBlank(byte[] data, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (data[offset + i] != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteEnable() => SpiTransfer([0x06]);

    private static bool IsBusy()
    {
        var status = SpiTransfer([0x05, 0x00]);
        return (status[1] & 0x01) != 0;
    }

    private static byte ReadStatus(byte command) => SpiTransfer([command, 0x00])[1];

    private static async Task ClearSpiNorProtectionAsync(IProgress<int> progress)
    {
        var sr1 = ReadStatus(0x05);
        var sr2 = ReadStatus(0x35);
        progress.Report(20);

        var nextSr1 = (byte)(sr1 & 0x03);
        var nextSr2 = (byte)(sr2 & 0x02);
        if (nextSr1 == sr1 && nextSr2 == sr2)
        {
            progress.Report(100);
            return;
        }

        WriteEnable();
        SpiTransfer([0x01, nextSr1, nextSr2]);
        await WaitUntilReadyAsync();
        progress.Report(100);
    }

    private static async Task WaitUntilReadyAsync()
    {
        for (var i = 0; i < 500; i++)
        {
            if (!IsBusy())
            {
                return;
            }

            await Task.Delay(PageProgramDelayMs);
        }

        throw new TimeoutException("Write timeout. Chip still reports WIP=1.");
    }

    private static void WriteAddress(byte[] buffer, int offset, byte command3Byte, byte command4Byte, int address, int addressBytes)
    {
        buffer[offset] = addressBytes == 4 ? command4Byte : command3Byte;
        if (addressBytes == 4)
        {
            buffer[offset + 1] = (byte)((address >> 24) & 0xFF);
            buffer[offset + 2] = (byte)((address >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((address >> 8) & 0xFF);
            buffer[offset + 4] = (byte)(address & 0xFF);
            return;
        }

        buffer[offset + 1] = (byte)((address >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((address >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(address & 0xFF);
    }

    private static bool Uses4ByteAddress(ChipProfile chip, int address) =>
        chip.SizeBytes > 0x1000000 || address > 0xFFFFFF;

    private static bool IsI2c(ChipProfile chip) => string.Equals(chip.Protocol, "I2C", StringComparison.OrdinalIgnoreCase);

    private static void EnsureSpi(ChipProfile chip)
    {
        if (!string.Equals(chip.Protocol, "SPI", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("This protocol is catalog-only in the CH347 backend. Real read/write is enabled for SPI 25xx and I2C 24xx.");
        }
    }

    private sealed class Ch347Device : IDisposable
    {
        public void Dispose() => NativeMethods.CH347CloseDevice(DeviceIndex);
    }

    private static class NativeMethods
    {
        [DllImport("CH347DLLA64.DLL", EntryPoint = "CH347OpenDevice", CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr CH347OpenDevice(int index);

        [DllImport("CH347DLLA64.DLL", EntryPoint = "CH347CloseDevice", CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH347CloseDevice(int index);

        [DllImport("CH347DLLA64.DLL", EntryPoint = "CH347StreamSPI4", CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH347StreamSPI4(int index, uint chipSelect, uint length, byte[] buffer);

        [DllImport("CH347DLLA64.DLL", EntryPoint = "CH347StreamI2C", CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CH347StreamI2C(int index, uint writeLength, byte[] writeBuffer, uint readLength, byte[] readBuffer);
    }
}

public sealed class ChNativeProgrammer : IChipProgrammer
{
    private const int DeviceIndex = 0;
    private const string ChNativeDll = "CH" + "341DLLA64.DLL";
    private const uint StreamMode = 0x81;
    private const uint ChipSelect = 0x80;
    private const int ReadChunkSize = 3840;
    private const int I2cReadChunkSize = 256;
    private const int PageProgramDelayMs = 1;

    public string Name => "CH341 native DLL";

    public static bool IsAvailable =>
        File.Exists(Path.Combine(Environment.SystemDirectory, ChNativeDll)) ||
        File.Exists(Path.Combine(AppContext.BaseDirectory, ChNativeDll));

    public static bool CanOpenDevice()
    {
        var handle = NativeMethods.CHOpenDevice(DeviceIndex);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        NativeMethods.CHCloseDevice(DeviceIndex);
        return true;
    }

    public async Task<bool> DetectAsync(IProgress<int> progress)
    {
        progress.Report(10);
        await Task.Yield();
        var handle = NativeMethods.CHOpenDevice(DeviceIndex);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            progress.Report(100);
            return false;
        }

        try
        {
            var ok = NativeMethods.CHSetStream(DeviceIndex, StreamMode);
            progress.Report(100);
            return ok;
        }
        finally
        {
            NativeMethods.CHCloseDevice(DeviceIndex);
        }
    }

    public Task<byte[]> ReadIdAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(() =>
    {
        if (IsI2c(chip))
        {
            progress.Report(100);
            return Array.Empty<byte>();
        }

        EnsureSpi(chip);
        using var device = OpenDevice();
        progress.Report(25);
        var id = SpiTransfer([0x9F, 0x00, 0x00, 0x00]);
        progress.Report(100);
        return id.Skip(1).Take(3).ToArray();
    });

    public Task<byte[]> ReadAsync(ChipProfile chip, int startAddress, int length, IProgress<int> progress) => Task.Run(() =>
    {
        if (IsI2c(chip))
        {
            using var i2cDevice = OpenDevice();
            return ReadI2cEeprom(chip, startAddress, length, progress);
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        var result = new byte[length];
        var done = 0;

        while (done < length)
        {
            var count = Math.Min(ReadChunkSize, length - done);
            var address = startAddress + done;
            var addressBytes = Uses4ByteAddress(chip, address) ? 4 : 3;
            var command = new byte[count + 1 + addressBytes];
            WriteAddress(command, 0, 0x03, 0x13, address, addressBytes);
            var response = SpiTransfer(command);
            Buffer.BlockCopy(response, command.Length - count, result, done, count);
            done += count;
            progress.Report(length == 0 ? 100 : done * 100 / length);
        }

        progress.Report(100);
        return result;
    });

    public Task WriteAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress, bool skipBlankPages = false) => Task.Run(async () =>
    {
        if (IsI2c(chip))
        {
            using var i2cDevice = OpenDevice();
            await WriteI2cEepromAsync(chip, startAddress, data, progress, skipBlankPages);
            return;
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        var done = 0;

        while (done < data.Length)
        {
            var pageOffset = (startAddress + done) % chip.PageSize;
            var count = Math.Min(chip.PageSize - pageOffset, data.Length - done);
            if (skipBlankPages && IsBlank(data, done, count))
            {
                done += count;
                progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
                continue;
            }

            WriteEnable();

            var address = startAddress + done;
            var addressBytes = Uses4ByteAddress(chip, address) ? 4 : 3;
            var headerLength = 1 + addressBytes;
            var command = new byte[count + headerLength];
            WriteAddress(command, 0, 0x02, 0x12, address, addressBytes);
            Buffer.BlockCopy(data, done, command, headerLength, count);
            SpiTransfer(command);
            await WaitUntilReadyAsync();

            done += count;
            progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
        }

        progress.Report(100);
    });

    public async Task<bool> VerifyAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress)
    {
        var actual = await ReadAsync(chip, startAddress, data.Length, progress);
        return actual.SequenceEqual(data);
    }

    public Task UnprotectAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(async () =>
    {
        if (IsI2c(chip))
        {
            throw new NotSupportedException("I2C EEPROM does not use SPI NOR block-protect status bits.");
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        await ClearSpiNorProtectionAsync(progress);
    });

    public Task EraseAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(async () =>
    {
        if (IsI2c(chip))
        {
            using var i2cDevice = OpenDevice();
            var blank = Enumerable.Repeat((byte)0xFF, chip.SizeBytes).ToArray();
            await WriteI2cEepromAsync(chip, 0, blank, progress, skipBlankPages: false);
            return;
        }

        EnsureSpi(chip);
        using var spiDevice = OpenDevice();
        WriteEnable();
        SpiTransfer([0xC7]);
        progress.Report(5);

        for (var i = 0; i < 600; i++)
        {
            if (!IsBusy())
            {
                progress.Report(100);
                return;
            }

            progress.Report(Math.Min(95, 5 + i / 7));
            await Task.Delay(100);
        }

        throw new TimeoutException("Erase timeout. Chip still reports WIP=1.");
    });

    private static ChDevice OpenDevice()
    {
        var handle = NativeMethods.CHOpenDevice(DeviceIndex);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            throw new InvalidOperationException("Cannot open CH. Check USB connection, WCH driver, and that no other programmer software is using it.");
        }

        if (!NativeMethods.CHSetStream(DeviceIndex, StreamMode))
        {
            NativeMethods.CHCloseDevice(DeviceIndex);
            throw new InvalidOperationException("Cannot configure CH SPI stream mode.");
        }

        return new ChDevice();
    }

    private static byte[] SpiTransfer(byte[] buffer)
    {
        var io = buffer.ToArray();
        if (!NativeMethods.CHStreamSPI4(DeviceIndex, ChipSelect, (uint)io.Length, io))
        {
            if (!NativeMethods.CHStreamSPI4(DeviceIndex, 0, (uint)io.Length, io))
            {
                throw new IOException("CH SPI transfer failed.");
            }
        }

        return io;
    }

    private static byte[] ReadI2cEeprom(ChipProfile chip, int startAddress, int length, IProgress<int> progress)
    {
        var result = new byte[length];
        var done = 0;
        while (done < length)
        {
            var count = Math.Min(I2cReadChunkSize, length - done);
            var address = startAddress + done;
            var write = BuildI2cAddressWriteBuffer(chip, address);
            var read = new byte[count];

            if (!NativeMethods.CHStreamI2C(DeviceIndex, (uint)write.Length, write, (uint)read.Length, read))
            {
                throw new IOException($"CH I2C read failed at 0x{address:X6}.");
            }

            Buffer.BlockCopy(read, 0, result, done, count);
            done += count;
            progress.Report(length == 0 ? 100 : done * 100 / length);
        }

        progress.Report(100);
        return result;
    }

    private static async Task WriteI2cEepromAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress, bool skipBlankPages)
    {
        var done = 0;
        while (done < data.Length)
        {
            var address = startAddress + done;
            var pageOffset = address % chip.PageSize;
            var count = Math.Min(chip.PageSize - pageOffset, data.Length - done);
            if (skipBlankPages && IsBlank(data, done, count))
            {
                done += count;
                progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
                continue;
            }

            var write = BuildI2cPageWriteBuffer(chip, address, data, done, count);

            if (!NativeMethods.CHStreamI2C(DeviceIndex, (uint)write.Length, write, 0, Array.Empty<byte>()))
            {
                throw new IOException($"CH I2C write failed at 0x{address:X6}.");
            }

            done += count;
            progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
            await Task.Delay(8);
        }

        progress.Report(100);
    }

    private static byte[] BuildI2cAddressWriteBuffer(ChipProfile chip, int address)
    {
        var device = I2cDeviceWriteAddress(chip, address);
        if (UsesOneByteI2cAddress(chip))
        {
            return [(byte)device, (byte)(address & 0xFF)];
        }

        return [(byte)device, (byte)((address >> 8) & 0xFF), (byte)(address & 0xFF)];
    }

    private static byte[] BuildI2cPageWriteBuffer(ChipProfile chip, int address, byte[] data, int dataOffset, int count)
    {
        var prefix = BuildI2cAddressWriteBuffer(chip, address);
        var write = new byte[prefix.Length + count];
        Buffer.BlockCopy(prefix, 0, write, 0, prefix.Length);
        Buffer.BlockCopy(data, dataOffset, write, prefix.Length, count);
        return write;
    }

    private static int I2cDeviceWriteAddress(ChipProfile chip, int address)
    {
        var block = UsesOneByteI2cAddress(chip) ? (address >> 8) & 0x07 : 0;
        return 0xA0 | (block << 1);
    }

    private static bool UsesOneByteI2cAddress(ChipProfile chip) => chip.SizeBytes <= 2048;

    private static bool IsBlank(byte[] data, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (data[offset + i] != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteEnable() => SpiTransfer([0x06]);

    private static bool IsBusy()
    {
        var status = SpiTransfer([0x05, 0x00]);
        return (status[1] & 0x01) != 0;
    }

    private static byte ReadStatus(byte command) => SpiTransfer([command, 0x00])[1];

    private static async Task ClearSpiNorProtectionAsync(IProgress<int> progress)
    {
        var sr1 = ReadStatus(0x05);
        var sr2 = ReadStatus(0x35);
        progress.Report(20);

        var nextSr1 = (byte)(sr1 & 0x03);
        var nextSr2 = (byte)(sr2 & 0x02);
        if (nextSr1 == sr1 && nextSr2 == sr2)
        {
            progress.Report(100);
            return;
        }

        WriteEnable();
        SpiTransfer([0x01, nextSr1, nextSr2]);
        await WaitUntilReadyAsync();
        progress.Report(100);
    }

    private static async Task WaitUntilReadyAsync()
    {
        for (var i = 0; i < 500; i++)
        {
            if (!IsBusy())
            {
                return;
            }

            await Task.Delay(PageProgramDelayMs);
        }

        throw new TimeoutException("Write timeout. Chip still reports WIP=1.");
    }

    private static void WriteAddress(byte[] buffer, int offset, byte command3Byte, byte command4Byte, int address, int addressBytes)
    {
        buffer[offset] = addressBytes == 4 ? command4Byte : command3Byte;
        if (addressBytes == 4)
        {
            buffer[offset + 1] = (byte)((address >> 24) & 0xFF);
            buffer[offset + 2] = (byte)((address >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((address >> 8) & 0xFF);
            buffer[offset + 4] = (byte)(address & 0xFF);
            return;
        }

        buffer[offset + 1] = (byte)((address >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((address >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(address & 0xFF);
    }

    private static bool Uses4ByteAddress(ChipProfile chip, int address) =>
        chip.SizeBytes > 0x1000000 || address > 0xFFFFFF;

    private static bool IsI2c(ChipProfile chip) => string.Equals(chip.Protocol, "I2C", StringComparison.OrdinalIgnoreCase);

    private static void EnsureSpi(ChipProfile chip)
    {
        if (!string.Equals(chip.Protocol, "SPI", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("This protocol is catalog-only in the CH backend. Real read/write is enabled for SPI 25xx and I2C 24xx.");
        }
    }

    private sealed class ChDevice : IDisposable
    {
        public void Dispose() => NativeMethods.CHCloseDevice(DeviceIndex);
    }

    private static class NativeMethods
    {
        [DllImport(ChNativeDll, EntryPoint = "CH" + "341OpenDevice", CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr CHOpenDevice(int index);

        [DllImport(ChNativeDll, EntryPoint = "CH" + "341CloseDevice", CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CHCloseDevice(int index);

        [DllImport(ChNativeDll, EntryPoint = "CH" + "341SetStream", CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CHSetStream(int index, uint mode);

        [DllImport(ChNativeDll, EntryPoint = "CH" + "341StreamSPI4", CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CHStreamSPI4(int index, uint chipSelect, uint length, byte[] buffer);

        [DllImport(ChNativeDll, EntryPoint = "CH" + "341StreamI2C", CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CHStreamI2C(int index, uint writeLength, byte[] writeBuffer, uint readLength, byte[] readBuffer);
    }
}

public sealed class MockCh34xProgrammer : IChipProgrammer
{
    public string Name => "mock CH341";

    public async Task<bool> DetectAsync(IProgress<int> progress)
    {
        await SimulateAsync(progress, 250);
        return false;
    }

    public async Task<byte[]> ReadIdAsync(ChipProfile chip, IProgress<int> progress)
    {
        await SimulateAsync(progress, 300);
        return chip.CommandSet switch
        {
            "25xx" => [0xEF, 0x40, ChipDensityCode(chip.SizeBytes)],
            "24xx" => [0x50, 0x00, 0x00],
            _ => [0x93, 0x00, 0x00]
        };
    }

    public async Task<byte[]> ReadAsync(ChipProfile chip, int startAddress, int length, IProgress<int> progress)
    {
        var data = new byte[length];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)((startAddress + i) & 0xFF);
            if (i % 4096 == 0)
            {
                progress.Report(data.Length == 0 ? 100 : i * 100 / data.Length);
                await Task.Delay(1);
            }
        }

        progress.Report(100);
        return data;
    }

    public Task WriteAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress, bool skipBlankPages = false) =>
        SimulateBlocksAsync(data.Length, progress);

    public async Task<bool> VerifyAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress)
    {
        await SimulateBlocksAsync(data.Length, progress);
        return true;
    }

    public Task UnprotectAsync(ChipProfile chip, IProgress<int> progress) => SimulateAsync(progress, 200);

    public Task EraseAsync(ChipProfile chip, IProgress<int> progress) => SimulateAsync(progress, 700);

    private static async Task SimulateBlocksAsync(int length, IProgress<int> progress)
    {
        var blocks = Math.Max(1, length / 4096);
        for (var i = 0; i <= blocks; i++)
        {
            progress.Report(i * 100 / blocks);
            await Task.Delay(4);
        }
    }

    private static async Task SimulateAsync(IProgress<int> progress, int durationMs)
    {
        for (var i = 0; i <= 10; i++)
        {
            progress.Report(i * 10);
            await Task.Delay(durationMs / 10);
        }
    }

    private static byte ChipDensityCode(int sizeBytes) => sizeBytes switch
    {
        <= 1024 * 1024 => 0x14,
        <= 2 * 1024 * 1024 => 0x15,
        <= 4 * 1024 * 1024 => 0x16,
        <= 8 * 1024 * 1024 => 0x17,
        _ => 0x18
    };
}
