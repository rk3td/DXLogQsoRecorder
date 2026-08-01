using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DXLogQsoRecorder.Services;

public sealed class UdpListenerService : IAsyncDisposable
{
    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public event Action<string, IPEndPoint>? PacketReceived;
    public event Action<Exception>? ListenerError;

    public void Start(string bindAddress, int port)
    {
        if (_client is not null) throw new InvalidOperationException("The UDP listener is already running.");
        var ip = IPAddress.Parse(bindAddress);
        _client = new UdpClient(new IPEndPoint(ip, port));
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _client is not null)
            {
                var result = await _client.ReceiveAsync(token);
                var text = Encoding.UTF8.GetString(result.Buffer);
                PacketReceived?.Invoke(text, result.RemoteEndPoint);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { ListenerError?.Invoke(ex); }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _client?.Dispose();
        if (_loopTask is not null)
        {
            try { await _loopTask; } catch { }
        }
        _loopTask = null; _client = null;
        _cts?.Dispose(); _cts = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
