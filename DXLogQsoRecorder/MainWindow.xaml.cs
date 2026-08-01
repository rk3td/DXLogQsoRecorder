using System.Collections.ObjectModel;
using System.IO;
using System.Net;
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

    public MainWindow()
    {
        InitializeComponent();
        PortablePaths.EnsureCreated();
        PortablePaths.CleanupTempFiles();
        _settings = _settingsService.Load();
        _recordings = new RecordingCoordinator(_audio, () => _settings);
        RecordingsGrid.ItemsSource = _items;
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
        _recordings.RecordingCompleted += (q, path) => Dispatcher.Invoke(() => AddOrUpdate(q, Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase) ? "Completed (WAV)" : "Completed", path));
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
            Dispatcher.Invoke(() => CurrentQsoText.Text = $"{qso.Timestamp:HH:mm:ss}  {qso.MyCall} – {qso.Call}  {qso.Band} MHz  {qso.Mode}");
            _recordings.Begin(qso);
        }
        catch (Exception ex)
        {
            LogService.Write("Packet processing error: " + ex);
            Dispatcher.Invoke(() => SetError("QSO processing: " + ex.Message));
        }
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

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow { Owner = this };
        window.ShowDialog();
    }

    protected override async void OnClosed(EventArgs e)
    {
        await StopAsync();
        _recordings.Dispose();
        _audio.Dispose();
        await _udp.DisposeAsync();
        base.OnClosed(e);
    }
}
