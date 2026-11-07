using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A minimal HTTP and WebSocket server on the loopback interface for car link tests: one
/// WebSocket client at a time, text and binary frames, and canned HTTP replies.
/// </summary>
public sealed class CarLinkLoopbackServer : IDisposable
{
    /// <summary>
    /// An HTTP request as the server read it.
    /// </summary>
    public sealed class Request
    {
        public string Method;
        public string Path;
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Body;
    }

    /// <summary>
    /// An HTTP reply.
    /// </summary>
    public sealed class Response
    {
        public int Status = 200;
        public string Reason = "OK";
        public Dictionary<string, string> Headers = new Dictionary<string, string>();
        public byte[] Body = new byte[0];

        public static Response Json(string json, int status = 200, string reason = "OK")
        {
            Response response = new Response { Status = status, Reason = reason, Body = Encoding.UTF8.GetBytes(json) };
            response.Headers["Content-Type"] = "application/json";
            return response;
        }
    }

    public int Port { get; }

    /// <summary>
    /// Text frames received from the WebSocket client, in order.
    /// </summary>
    public BlockingCollection<string> Received { get; } = new BlockingCollection<string>();

    /// <summary>
    /// Answers HTTP requests; 404 when null.
    /// </summary>
    public Func<Request, Response> Http;

    public bool HasClient { get { return this.client != null; } }

    public CarLinkLoopbackServer()
    {
        this.listener = new TcpListener(IPAddress.Loopback, 0);
        this.listener.Start();
        this.Port = ((IPEndPoint)this.listener.LocalEndpoint).Port;
        this.acceptLoop = Task.Run(this.AcceptLoopAsync);
    }

    public void SendText(string text)
    {
        this.SendFrame(0x1, Encoding.UTF8.GetBytes(text));
    }

    public void SendBinary(byte[] data)
    {
        this.SendFrame(0x2, data);
    }

    /// <summary>
    /// Closes the WebSocket client's connection without a close frame.
    /// </summary>
    public void DropClient()
    {
        TcpClient current = this.client;
        this.client = null;
        current?.Close();
    }

    public void Dispose()
    {
        this.stopped = true;
        this.listener.Stop();
        this.DropClient();
    }

    private const string webSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener listener;
    private readonly Task acceptLoop;
    private readonly object sendLock = new object();
    private volatile TcpClient client;
    private volatile bool stopped;

    private async Task AcceptLoopAsync()
    {
        while (!this.stopped)
        {
            TcpClient accepted;
            try
            {
                accepted = await this.listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }
            _ = Task.Run(() => this.HandleAsync(accepted));
        }
    }

    private async Task HandleAsync(TcpClient connection)
    {
        try
        {
            NetworkStream stream = connection.GetStream();
            Request request = CarLinkLoopbackServer.ReadRequest(stream);
            if (request == null)
            {
                connection.Close();
                return;
            }
            if (request.Headers.TryGetValue("Upgrade", out string upgrade) && upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase))
            {
                string key = request.Headers["Sec-WebSocket-Key"];
                string accept;
                using (SHA1 sha = SHA1.Create())
                {
                    accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + CarLinkLoopbackServer.webSocketGuid)));
                }
                byte[] handshake = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
                stream.Write(handshake, 0, handshake.Length);
                this.client = connection;
                await Task.Run(() => this.ReadFrames(connection, stream)).ConfigureAwait(false);
                return;
            }
            Response response = this.Http?.Invoke(request) ?? Response.Json("{\"ok\": false, \"message\": \"not found\"}", 404, "Not Found");
            StringBuilder head = new StringBuilder();
            head.Append("HTTP/1.1 ").Append(response.Status).Append(' ').Append(response.Reason).Append("\r\n");
            foreach (KeyValuePair<string, string> header in response.Headers)
            {
                head.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }
            head.Append("Content-Length: ").Append(response.Body.Length).Append("\r\nConnection: close\r\n\r\n");
            byte[] headBytes = Encoding.ASCII.GetBytes(head.ToString());
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(response.Body, 0, response.Body.Length);
            stream.Flush();
            connection.Close();
        }
        catch (Exception)
        {
            connection.Close();
        }
    }

    private void ReadFrames(TcpClient connection, NetworkStream stream)
    {
        try
        {
            while (true)
            {
                int first = stream.ReadByte();
                int second = stream.ReadByte();
                if (first < 0 || second < 0)
                {
                    return;
                }
                int opcode = first & 0x0f;
                long length = second & 0x7f;
                if (length == 126)
                {
                    length = (CarLinkLoopbackServer.ReadExactly(stream, 1)[0] << 8) | CarLinkLoopbackServer.ReadExactly(stream, 1)[0];
                }
                else if (length == 127)
                {
                    byte[] bytes = CarLinkLoopbackServer.ReadExactly(stream, 8);
                    length = 0;
                    foreach (byte b in bytes)
                    {
                        length = (length << 8) | b;
                    }
                }
                byte[] mask = (second & 0x80) != 0 ? CarLinkLoopbackServer.ReadExactly(stream, 4) : null;
                byte[] payload = CarLinkLoopbackServer.ReadExactly(stream, (int)length);
                if (mask != null)
                {
                    for (int i = 0; i < payload.Length; i++)
                    {
                        payload[i] ^= mask[i % 4];
                    }
                }
                if (opcode == 0x1)
                {
                    this.Received.Add(Encoding.UTF8.GetString(payload));
                }
                else if (opcode == 0x8)
                {
                    this.SendFrame(0x8, new byte[0]);
                    connection.Close();
                    return;
                }
                else if (opcode == 0x9)
                {
                    this.SendFrame(0xA, payload);
                }
            }
        }
        catch (Exception)
        {
            // The client went away
        }
    }

    private void SendFrame(int opcode, byte[] payload)
    {
        TcpClient current = this.client;
        if (current == null)
        {
            throw new InvalidOperationException("no WebSocket client");
        }
        lock (this.sendLock)
        {
            NetworkStream stream = current.GetStream();
            List<byte> head = new List<byte> { (byte)(0x80 | opcode) };
            if (payload.Length < 126)
            {
                head.Add((byte)payload.Length);
            }
            else if (payload.Length <= 0xffff)
            {
                head.Add(126);
                head.Add((byte)(payload.Length >> 8));
                head.Add((byte)payload.Length);
            }
            else
            {
                head.Add(127);
                for (int shift = 56; shift >= 0; shift -= 8)
                {
                    head.Add((byte)((long)payload.Length >> shift));
                }
            }
            stream.Write(head.ToArray(), 0, head.Count);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }
    }

    private static Request ReadRequest(NetworkStream stream)
    {
        List<byte> head = new List<byte>();
        while (head.Count < 4 || head[head.Count - 4] != '\r' || head[head.Count - 3] != '\n' || head[head.Count - 2] != '\r' || head[head.Count - 1] != '\n')
        {
            int b = stream.ReadByte();
            if (b < 0)
            {
                return null;
            }
            head.Add((byte)b);
        }
        string[] lines = Encoding.ASCII.GetString(head.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        string[] requestLine = lines[0].Split(' ');
        Request request = new Request { Method = requestLine[0], Path = requestLine[1] };
        for (int i = 1; i < lines.Length; i++)
        {
            int colon = lines[i].IndexOf(':');
            if (colon > 0)
            {
                request.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }
        }
        if (request.Headers.TryGetValue("Content-Length", out string lengthText) && int.TryParse(lengthText, out int length) && length > 0)
        {
            request.Body = Encoding.UTF8.GetString(CarLinkLoopbackServer.ReadExactly(stream, length));
        }
        return request;
    }

    private static byte[] ReadExactly(Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
            {
                throw new EndOfStreamException();
            }
            offset += read;
        }
        return buffer;
    }
}
