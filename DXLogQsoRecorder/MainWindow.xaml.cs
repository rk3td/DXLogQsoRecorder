using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Text;
using System.Windows;
using DXLogQsoRecorder.Models;
using DXLogQsoRecorder.Services;

namespace DXLogQsoRecorder;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly DxLogXmlParser _parser = new();
    private readonly UdpListenerService _udp = new();
    private readonly AudioCaptureService _audio = new();
    private readonly ObservableCollection<RecordingItem> _items = new();
    private readonly RecordingCoordinator _recordings;
    private AppSettings _settings;
    private bool _running;
    private readonly RecordingIndexService _index;
    private readonly AudioPlaybackService _playback = new();
    private readonly ObservableCollection<RecordingBrowserItem> _browserItems = new();
    private readonly DispatcherTimer _playbackTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _browserLoaded;
    private bool _updatingPlaybackSlider;
    private readonly object _stationSync = new();
    private string? _activeStationId;
    private bool _recordAllStations;
    private StationConflictWindow? _stationConflictWindow;
    private DxLogQso? _pendingStationQso;
    private string? _pendingNewStationId;
    private bool _stationChoiceResolvedForSession;

    public MainWindow()
    {
        InitializeComponent();
        PortablePaths.EnsureCreated();
        PortablePaths.CleanupTempFiles();
        _index = new RecordingIndexService();
        _settings = _settingsService.Load();
        _recordings = new RecordingCoordinator(_audio, () => _settings);
        RecordingsGrid.ItemsSource = _items;
        BrowserGrid.ItemsSource = _browserItems;
        ContestFilterCombo.ItemsSource = new[] { "All contests" };
        ContestFilterCombo.SelectedIndex = 0;
        _playbackTimer.Tick += PlaybackTimer_Tick;
        _playback.PlaybackStopped += () => Dispatcher.BeginInvoke(ResetPlaybackUi);
        LoadSettingsToUi();
        LoadAudioDevices();
        HookEvents();
    }

    private void HookEvents()
    {
        _udp.PacketReceived += OnPacketReceived;
        _udp.ListenerError += ex => Dispatcher.Invoke(() => SetError("UDP: " + ex.Message));
        _audio.LevelChanged += level => Dispatcher.BeginInvoke(() => LevelBar.Value = level * 100);
        _recordings.RecordingStarted += (q, path) => Dispatcher.Invoke(() => AddOrUpdate(q, "Recording...", path));
        _recordings.RecordingCompleted += (q, path) => Dispatcher.InvokeAsync(async () =>
        {
            AddOrUpdate(q, Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase) ? "Completed (WAV)" : "Completed", path);
            try { await _index.UpsertAsync(path, q); if (_browserLoaded) await LoadBrowserAsync(); }
            catch (Exception ex) { LogService.Write("[WARNING] Index update failed: " + ex.Message); }
        });
        _recordings.Mp3EncodingFailed += (q, path, ex) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = $"MP3 encoding failed; the {q.Call} recording was saved as WAV.";
            LogService.Write($"[WARNING] WAV fallback shown to user: {path}");
        });
        _recordings.RecordingFailed += (q, ex) => Dispatcher.Invoke(() => AddOrUpdate(q, "Error: " + ex.Message, ""));
    }

    private void LoadSettingsToUi()
    {
        BindAddressBox.Text = _settings.BindAddress;
        UdpPortBox.Text = _settings.UdpPort.ToString();
        PreBufferBox.Text = _settings.PreBufferSeconds.ToString();
        PostBufferBox.Text = _settings.PostBufferSeconds.ToString();
        OutputDirectoryBox.Text = _settings.OutputDirectory;
        foreach (System.Windows.Controls.ComboBoxItem item in BitrateCombo.Items)
            if (item.Content?.ToString() == _settings.Mp3BitrateKbps.ToString()) BitrateCombo.SelectedItem = item;
    }

    private void LoadAudioDevices()
    {
        try
        {
            var devices = _audio.GetDevices();
            AudioDeviceCombo.ItemsSource = devices;
            AudioDeviceCombo.SelectedItem = devices.FirstOrDefault(d => d.Id == _settings.AudioDeviceId) ?? devices.FirstOrDefault();
            if (devices.Count == 0) StatusText.Text = "No active recording devices were found.";
        }
        catch (Exception ex) { SetError("Unable to enumerate audio devices: " + ex.Message); }
    }

    private bool TryReadSettings(out AppSettings settings)
    {
        settings = new AppSettings();
        if (!IPAddress.TryParse(BindAddressBox.Text.Trim(), out _)) { MessageBox.Show("Invalid bind address."); return false; }
        if (!int.TryParse(UdpPortBox.Text, out var port) || port is < 1 or > 65535) { MessageBox.Show("Invalid UDP port."); return false; }
        if (!int.TryParse(PreBufferBox.Text, out var pre) || pre is < 1 or > 600) { MessageBox.Show("Pre-buffer must be between 1 and 600 seconds."); return false; }
        if (!int.TryParse(PostBufferBox.Text, out var post) || post is < 0 or > 600) { MessageBox.Show("Post-buffer must be between 0 and 600 seconds."); return false; }
        if (AudioDeviceCombo.SelectedItem is not AudioDeviceInfo device) { MessageBox.Show("Select an audio device."); return false; }
        if (string.IsNullOrWhiteSpace(OutputDirectoryBox.Text)) { MessageBox.Show("Specify a recording directory."); return false; }
        if (BitrateCombo.SelectedItem is not System.Windows.Controls.ComboBoxItem bitrateItem ||
            !int.TryParse(bitrateItem.Content?.ToString(), out var bitrate) || bitrate is not (32 or 48 or 64))
        { MessageBox.Show("Select an MP3 bitrate."); return false; }
        settings = new AppSettings
        {
            BindAddress = BindAddressBox.Text.Trim(), UdpPort = port, AudioDeviceId = device.Id,
            PreBufferSeconds = pre, PostBufferSeconds = post, OutputDirectory = OutputDirectoryBox.Text.Trim(),
            Mp3BitrateKbps = bitrate, SaveRawPackets = true
        };
        return true;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out _settings)) return;
        try
        {
            _settingsService.Save(_settings);
            var device = (AudioDeviceInfo)AudioDeviceCombo.SelectedItem;
            _audio.Start(device.Id, _settings.PreBufferSeconds);
            _udp.Start(_settings.BindAddress, _settings.UdpPort);
            _running = true;
            ResetStationSession();
            SetControls(true);
            UdpStatusText.Text = $"Listening on {_settings.BindAddress}:{_settings.UdpPort}";
            StatusText.Text = "Audio buffering and DXLog reception started.";
            LogService.Write("Recorder started.");
        }
        catch (Exception ex)
        {
            await _udp.StopAsync();
            _audio.Stop();
            SetError(ex.Message);
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e) => await StopAsync();

    private async Task StopAsync()
    {
        if (!_running) return;
        _running = false;
        ResetStationSession();
        await _udp.StopAsync();
        _audio.Stop();
        SetControls(false);
        UdpStatusText.Text = "Stopped";
        LevelBar.Value = 0;
        StatusText.Text = "Stopped.";
        LogService.Write("Recorder stopped.");
    }

    private void OnPacketReceived(string xml, IPEndPoint endpoint)
    {
        try
        {
            if (_settings.SaveRawPackets) SaveRawPacket(xml, endpoint);
            if (!_parser.TryParse(xml, out var qso, out var error) || qso is null)
            {
                LogService.Write("Ignored packet: " + error);
                return;
            }
            if (!qso.IsNewQso)
            {
                LogService.Write($"Ignored non-new QSO: {qso.QsoId} {qso.Call}");
                return;
            }
            HandleStationFilteredQso(qso);
        }
        catch (Exception ex)
        {
            LogService.Write("Packet processing error: " + ex);
            Dispatcher.Invoke(() => SetError("QSO processing: " + ex.Message));
        }
    }

    private void HandleStationFilteredQso(DxLogQso qso)
    {
        var stationId = NormalizeStationId(qso.StationId);
        var shouldRecord = false;
        var shouldShowConflict = false;
        string? currentStation = null;

        lock (_stationSync)
        {
            if (_recordAllStations)
            {
                shouldRecord = true;
            }
            else if (string.IsNullOrWhiteSpace(stationId))
            {
                // DXLog packets without stationid cannot be filtered reliably.
                // Record them, but do not use them to establish the active station.
                shouldRecord = true;
                LogService.Write($"[WARNING] QSO {qso.Call} has no stationid; recorded without station filtering.");
            }
            else if (string.IsNullOrWhiteSpace(_activeStationId))
            {
                _activeStationId = stationId;
                shouldRecord = true;
                currentStation = stationId;
                LogService.Write($"[INFO] Active station established: {stationId}");
            }
            else if (string.Equals(_activeStationId, stationId, StringComparison.OrdinalIgnoreCase))
            {
                shouldRecord = true;
            }
            else
            {
                currentStation = _activeStationId;

                // The operator is asked only once per recording session. After the
                // first decision (including the 20-second timeout), all future QSO
                // packets are handled according to that decision until Stop is pressed.
                if (_stationChoiceResolvedForSession)
                {
                    LogService.Write($"[INFO] QSO {qso.Call} from station {stationId} ignored by the locked station selection ({_activeStationId}).");
                }
                else if (_stationConflictWindow is null)
                {
                    _pendingStationQso = qso;
                    _pendingNewStationId = stationId;
                    shouldShowConflict = true;
                }
                else
                {
                    LogService.Write($"[INFO] QSO {qso.Call} from station {stationId} ignored while station selection is pending.");
                }
            }
        }

        if (shouldRecord)
        {
            BeginRecording(qso);
            if (!string.IsNullOrWhiteSpace(currentStation))
                Dispatcher.BeginInvoke(() => StationStatusText.Text = $"Station: {currentStation} only");
            return;
        }

        if (shouldShowConflict && currentStation is not null && stationId is not null)
            Dispatcher.BeginInvoke(() => ShowStationConflict(currentStation, stationId));
    }

    private void BeginRecording(DxLogQso qso)
    {
        Dispatcher.BeginInvoke(() =>
            CurrentQsoText.Text = $"{qso.Timestamp:HH:mm:ss}  {qso.MyCall} – {qso.Call}  {qso.Band} MHz  {qso.Mode}");
        _recordings.Begin(qso);
    }

    private void ShowStationConflict(string currentStation, string newStation)
    {
        lock (_stationSync)
        {
            if (!_running || _recordAllStations || _stationChoiceResolvedForSession || _stationConflictWindow is not null) return;
            _stationConflictWindow = new StationConflictWindow(currentStation, newStation) { Owner = this };
            _stationConflictWindow.SelectionCompleted += StationConflict_SelectionCompleted;
            _stationConflictWindow.Closed += (_, _) =>
            {
                lock (_stationSync)
                {
                    _stationConflictWindow = null;
                    _pendingStationQso = null;
                    _pendingNewStationId = null;
                }
            };
            _stationConflictWindow.Show();
        }
        LogService.Write($"[WARNING] Another DXLog station was detected. Current={currentStation}; New={newStation}");
    }

    private void StationConflict_SelectionCompleted(StationConflictChoice choice)
    {
        DxLogQso? qsoToRecord = null;
        string? selectedStation = null;

        lock (_stationSync)
        {
            _stationChoiceResolvedForSession = true;

            if (choice == StationConflictChoice.SwitchToNew && !string.IsNullOrWhiteSpace(_pendingNewStationId))
            {
                _activeStationId = _pendingNewStationId;
                selectedStation = _activeStationId;
                qsoToRecord = _pendingStationQso;
                LogService.Write($"[INFO] Active station switched to {_activeStationId}");
            }
            else if (choice == StationConflictChoice.RecordAll)
            {
                _recordAllStations = true;
                selectedStation = "All";
                qsoToRecord = _pendingStationQso;
                LogService.Write("[INFO] Station filter changed to all stations for the current recording session.");
            }
            else
            {
                selectedStation = _activeStationId;
                LogService.Write($"[INFO] Active station remains {_activeStationId}. The foreign-station QSO was ignored.");
            }

            _pendingStationQso = null;
            _pendingNewStationId = null;
        }

        StationStatusText.Text = selectedStation == "All" ? "Station: All" : $"Station: {selectedStation} only";
        if (qsoToRecord is not null) BeginRecording(qsoToRecord);
    }

    private void ResetStationSession()
    {
        StationConflictWindow? window;
        lock (_stationSync)
        {
            _activeStationId = null;
            _recordAllStations = false;
            _stationChoiceResolvedForSession = false;
            _pendingStationQso = null;
            _pendingNewStationId = null;
            window = _stationConflictWindow;
            _stationConflictWindow = null;
        }

        if (window is not null)
        {
            window.SelectionCompleted -= StationConflict_SelectionCompleted;
            window.Close();
        }

        if (Dispatcher.CheckAccess()) StationStatusText.Text = "Station: waiting for first QSO";
        else Dispatcher.BeginInvoke(() => StationStatusText.Text = "Station: waiting for first QSO");
    }

    private static string? NormalizeStationId(string? stationId)
    {
        var normalized = stationId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToUpperInvariant();
    }

    private static void SaveRawPacket(string xml, IPEndPoint endpoint)
    {
        var safe = endpoint.Address.ToString().Replace(':', '_');
        var name = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{safe}_{endpoint.Port}.xml";
        File.WriteAllText(Path.Combine(PortablePaths.PacketDirectory, name), xml, Encoding.UTF8);
    }

    private void AddOrUpdate(DxLogQso qso, string status, string path)
    {
        var qsoKey = !string.IsNullOrWhiteSpace(qso.Guid)
            ? qso.Guid
            : !string.IsNullOrWhiteSpace(qso.QsoId)
                ? qso.QsoId
                : $"{qso.Timestamp:O}|{qso.Call}|{qso.Band}|{qso.Mode}";
        var item = _items.FirstOrDefault(x => x.QsoKey == qsoKey);
        if (item is null)
        {
            item = new RecordingItem
            {
                QsoKey = qsoKey,
                ReceivedAt = DateTime.Now,
                Call = qso.Call,
                Band = qso.Band + " MHz",
                Mode = qso.Mode,
                Status = status,
                FileName = path
            };
            _items.Insert(0, item);
            while (_items.Count > 1000) _items.RemoveAt(_items.Count - 1);
        }
        else
        {
            item.Status = status;
            if (!string.IsNullOrWhiteSpace(path)) item.FileName = path;
        }
        RecordingsGrid.Items.Refresh();
    }

    private void SetControls(bool running)
    {
        StartButton.IsEnabled = !running; StopButton.IsEnabled = running;
        BindAddressBox.IsEnabled = !running; UdpPortBox.IsEnabled = !running;
        AudioDeviceCombo.IsEnabled = !running; PreBufferBox.IsEnabled = !running;
        PostBufferBox.IsEnabled = !running; BitrateCombo.IsEnabled = !running; OutputDirectoryBox.IsEnabled = !running;
    }

    private void SetError(string text)
    {
        StatusText.Text = "Error: " + text;
        LogService.Write("ERROR " + text);
        MessageBox.Show(text, "DXLog QSO Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private string GetRecordingsRoot()
    {
        var configured = string.IsNullOrWhiteSpace(OutputDirectoryBox.Text) ? _settings.OutputDirectory : OutputDirectoryBox.Text.Trim();
        return Path.IsPathRooted(configured) ? configured : Path.Combine(PortablePaths.BaseDirectory, configured);
    }

    private async void RecordingsTab_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_browserLoaded) return;
        _browserLoaded = true;
        await SynchronizeAndLoadBrowserAsync();
    }

    private async Task SynchronizeAndLoadBrowserAsync()
    {
        try
        {
            StatusText.Text = "Indexing recordings...";
            await _index.SynchronizeAsync(GetRecordingsRoot());
            await ReloadContestsAsync();
            await LoadBrowserAsync();
            StatusText.Text = "Recording index is up to date.";
        }
        catch (Exception ex) { SetError("Recording index: " + ex.Message); }
    }

    private async Task ReloadContestsAsync()
    {
        var selected = ContestFilterCombo.SelectedItem?.ToString() ?? "All contests";
        var contests = new List<string> { "All contests" };
        contests.AddRange(await _index.GetContestsAsync());
        ContestFilterCombo.ItemsSource = contests;
        ContestFilterCombo.SelectedItem = contests.Contains(selected) ? selected : "All contests";
    }

    private async Task LoadBrowserAsync()
    {
        var selectedContest = ContestFilterCombo.SelectedItem?.ToString();
        var results = await _index.SearchAsync(CallsignFilterBox.Text, selectedContest);
        _browserItems.Clear();
        foreach (var item in results) _browserItems.Add(item);
        ResultCountText.Text = $"{_browserItems.Count} recording{(_browserItems.Count == 1 ? "" : "s")}";
    }

    private async void SearchFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_browserLoaded || !IsLoaded) return;
        await LoadBrowserAsync();
    }

    private async void RefreshIndexButton_Click(object sender, RoutedEventArgs e) => await SynchronizeAndLoadBrowserAsync();

    private RecordingBrowserItem? SelectedBrowserItem => BrowserGrid.SelectedItem as RecordingBrowserItem;

    private void PlaySelected()
    {
        var item = SelectedBrowserItem;
        if (item is null) { StatusText.Text = "Select a recording first."; return; }
        if (!File.Exists(item.FilePath)) { StatusText.Text = "The selected file no longer exists. Refresh the index."; return; }
        try
        {
            _playback.Play(item.FilePath);
            PlaybackSlider.Maximum = Math.Max(0.1, _playback.TotalTime.TotalSeconds);
            _playbackTimer.Start();
            PlayButton.Content = "Playing";
            StatusText.Text = $"Playing {item.Callsign} — {item.Contest}";
        }
        catch (Exception ex) { SetError("Playback: " + ex.Message); }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e) => PlaySelected();
    private void BrowserGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => PlaySelected();
    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _playback.TogglePause();
        PauseButton.Content = _playback.IsPaused ? "Resume" : "Pause";
    }
    private void PlaybackStopButton_Click(object sender, RoutedEventArgs e) { _playback.Stop(); ResetPlaybackUi(); }
    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        _updatingPlaybackSlider = true;
        PlaybackSlider.Value = _playback.CurrentTime.TotalSeconds;
        PlaybackTimeText.Text = $"{_playback.CurrentTime:mm\\:ss} / {_playback.TotalTime:mm\\:ss}";
        _updatingPlaybackSlider = false;
    }
    private void PlaybackSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_updatingPlaybackSlider) _playback.Seek(TimeSpan.FromSeconds(PlaybackSlider.Value));
    }
    private void ResetPlaybackUi()
    {
        _playbackTimer.Stop();
        PlaybackSlider.Value = 0; PlaybackSlider.Maximum = 1;
        PlaybackTimeText.Text = "00:00 / 00:00"; PlayButton.Content = "Play"; PauseButton.Content = "Pause";
    }
    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var path = SelectedBrowserItem?.FilePath;
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\\\"{path}\\\"") { UseShellExecute = true });
            else
            {
                var root = GetRecordingsRoot(); Directory.CreateDirectory(root);
                Process.Start(new ProcessStartInfo(root) { UseShellExecute = true });
            }
        }
        catch (Exception ex) { SetError("Open folder: " + ex.Message); }
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow { Owner = this };
        window.ShowDialog();
    }

    protected override async void OnClosed(EventArgs e)
    {
        await StopAsync();
        _playback.Dispose();
        _playbackTimer.Stop();
        _recordings.Dispose();
        _audio.Dispose();
        await _udp.DisposeAsync();
        base.OnClosed(e);
    }
}
