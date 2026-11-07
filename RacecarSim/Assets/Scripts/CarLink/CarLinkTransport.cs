using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// One message received from the car: text (JSON) or binary (CBOR).
/// </summary>
public sealed class CarLinkFrame
{
    public string Text;
    public byte[] Binary;

    public bool IsBinary { get { return this.Binary != null; } }
}

/// <summary>
/// A message connection to the car's bridge. Sends are not concurrent: the caller sends from one
/// task at a time.
/// </summary>
public interface ICarLinkTransport : IDisposable
{
    bool IsOpen { get; }

    Task ConnectAsync(Uri uri, CancellationToken cancellation);

    Task SendTextAsync(string text, CancellationToken cancellation);

    /// <summary>
    /// The next whole message, or null when the car closed the connection.
    /// </summary>
    Task<CarLinkFrame> ReceiveAsync(CancellationToken cancellation);

    Task CloseAsync(CancellationToken cancellation);
}

/// <summary>
/// ICarLinkTransport over System.Net.WebSockets.ClientWebSocket.
/// </summary>
public sealed class WebSocketTransport : ICarLinkTransport
{
    #region Constants
    /// <summary>
    /// Largest message accepted (in bytes); a 50 x 50 m map at 5 cm is about 1 MB as CBOR.
    /// </summary>
    public const int MaxMessageBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Time allowed to open the connection (in seconds).
    /// </summary>
    public const double ConnectTimeout = 5;
    #endregion

    #region Public Interface
    public bool IsOpen { get { return this.socket != null && this.socket.State == WebSocketState.Open; } }

    public async Task ConnectAsync(Uri uri, CancellationToken cancellation)
    {
        this.socket?.Dispose();
        this.socket = new ClientWebSocket();
        this.socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(WebSocketTransport.ConnectTimeout));
            try
            {
                await this.socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new TimeoutException($"no answer from {uri.Host}:{uri.Port}");
            }
        }
    }

    public Task SendTextAsync(string text, CancellationToken cancellation)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return this.socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation);
    }

    public async Task<CarLinkFrame> ReceiveAsync(CancellationToken cancellation)
    {
        using (MemoryStream message = new MemoryStream())
        {
            while (true)
            {
                WebSocketReceiveResult result = await this.socket.ReceiveAsync(new ArraySegment<byte>(this.buffer), cancellation).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }
                if (message.Length + result.Count > WebSocketTransport.MaxMessageBytes)
                {
                    throw new CarLinkFormatException($"message over {WebSocketTransport.MaxMessageBytes} bytes");
                }
                message.Write(this.buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    return result.MessageType == WebSocketMessageType.Binary
                        ? new CarLinkFrame { Binary = message.ToArray() }
                        : new CarLinkFrame { Text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length) };
                }
            }
        }
    }

    public async Task CloseAsync(CancellationToken cancellation)
    {
        if (this.IsOpen)
        {
            try
            {
                await this.socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", cancellation).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is WebSocketException || exception is OperationCanceledException || exception is ObjectDisposedException)
            {
                // The car side may already be gone; the socket is disposed next
            }
        }
    }

    public void Dispose()
    {
        this.socket?.Dispose();
        this.socket = null;
    }
    #endregion

    private readonly byte[] buffer = new byte[64 * 1024];
    private ClientWebSocket socket;
}
