using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Why a call to the car's HTTP API failed.
/// </summary>
public enum CarApiFailure
{
    /// <summary>
    /// No route to the address, or the name did not resolve.
    /// </summary>
    NoRoute,

    /// <summary>
    /// The car answered but nothing listens on the port.
    /// </summary>
    Refused,

    Timeout,

    /// <summary>
    /// The car returned an error status; see StatusCode and the message.
    /// </summary>
    Http,

    /// <summary>
    /// The car is running another map command (HTTP 409).
    /// </summary>
    Busy,

    /// <summary>
    /// The reply could not be read.
    /// </summary>
    Format,

    /// <summary>
    /// A download did not match the car's SHA-256.
    /// </summary>
    Checksum,

    /// <summary>
    /// A map or file name the car would refuse.
    /// </summary>
    BadName
}

/// <summary>
/// A call to the car's HTTP API failed.
/// </summary>
public class CarApiException : Exception
{
    public CarApiFailure Failure { get; }

    /// <summary>
    /// The HTTP status for Http and Busy failures, else 0.
    /// </summary>
    public int StatusCode { get; }

    public CarApiException(CarApiFailure failure, string message, int statusCode = 0) : base(message)
    {
        this.Failure = failure;
        this.StatusCode = statusCode;
    }
}

/// <summary>
/// A map command for the car (POST /api/v1/action).
/// </summary>
public sealed class CarAction
{
    /// <summary>
    /// "start", "save", "stop", "load", or "recover".
    /// </summary>
    public string Action;

    public string Name;

    /// <summary>
    /// Saved map to extend (start only).
    /// </summary>
    public string From;

    public bool HasPose;
    public double PoseX;
    public double PoseY;
    public double PoseYaw;

    /// <summary>
    /// Search the whole map for the car (load only).
    /// </summary>
    public bool Global;

    /// <summary>
    /// "amcl" or "slam_toolbox" (load only); null for the car's default.
    /// </summary>
    public string Localizer;

    /// <summary>
    /// "none", "session", or "rgbd" (start only); null for the car's default.
    /// </summary>
    public string Record;
}

/// <summary>
/// Client for the car's versioned HTTP API (http://host:8084/api/v1/): car info, map service
/// state, map commands, saved maps, and file downloads with resume and checksum.
/// </summary>
/// <remarks>
/// Every call runs off the main thread and completes without capturing a context; await it from
/// the main thread to continue there.
/// </remarks>
public sealed class CarApiClient : IDisposable
{
    #region Constants
    /// <summary>
    /// Time allowed for a JSON request (in seconds).
    /// </summary>
    public const double RequestTimeout = 5;

    /// <summary>
    /// Client name sent with map commands, shown on the car as who started a run.
    /// </summary>
    public const string ClientName = "racecarsim";
    #endregion

    #region Public Interface
    public Uri BaseUri { get; }

    /// <param name="host">Car address or name, for example 10.42.0.1.</param>
    /// <param name="port">API port, 8084 by default on the car.</param>
    public CarApiClient(string host, int port)
    {
        this.BaseUri = new UriBuilder("http", host, port, "/api/v1/").Uri;
        this.http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<RacecarInfo> GetInfoAsync(CancellationToken cancellation = default)
    {
        CarLinkValue value = await this.GetJsonAsync("info", cancellation).ConfigureAwait(false);
        return CarApiClient.Parse(() => RacecarInfo.Parse(value));
    }

    public async Task<ApiState> GetStateAsync(CancellationToken cancellation = default)
    {
        CarLinkValue value = await this.GetJsonAsync("state", cancellation).ConfigureAwait(false);
        return CarApiClient.Parse(() => ApiState.Parse(value));
    }

    public async Task<List<MapEntry>> GetMapsAsync(CancellationToken cancellation = default)
    {
        CarLinkValue value = await this.GetJsonAsync("maps", cancellation).ConfigureAwait(false);
        return CarApiClient.Parse(() => MapEntry.ParseList(value));
    }

    public async Task<MapEntry> GetMapAsync(string name, CancellationToken cancellation = default)
    {
        CarApiClient.CheckName(name);
        CarLinkValue value = await this.GetJsonAsync("maps/" + name, cancellation).ConfigureAwait(false);
        return CarApiClient.Parse(() => MapEntry.Parse(value));
    }

    /// <summary>
    /// Sends a map command; returns the car's job id.
    /// </summary>
    public async Task<string> SendActionAsync(CarAction action, CancellationToken cancellation = default)
    {
        if (action.Name != null)
        {
            CarApiClient.CheckName(action.Name);
        }
        if (action.From != null)
        {
            CarApiClient.CheckName(action.From);
        }
        CarLinkValue result = await this.PostJsonAsync("action", CarApiClient.ActionBody(action), cancellation).ConfigureAwait(false);
        CarLinkValue job = result["job"];
        return job.IsNull ? null : (job.Kind == CarLinkValueKind.String ? job.AsString() : CarLinkJson.Write(job));
    }

    /// <summary>
    /// Downloads one file of a saved map to a path. A partial download (path + ".part") left by
    /// an earlier attempt resumes with a range request. The file is checked against the car's
    /// SHA-256 before it replaces the path.
    /// </summary>
    /// <param name="progress">Receives the fraction done, 0 to 1, when the size is known.</param>
    public async Task DownloadFileAsync(string map, string file, string path, IProgress<double> progress = null, CancellationToken cancellation = default)
    {
        CarApiClient.CheckName(map);
        if (!CarApiClient.IsAllowedFile(map, file))
        {
            throw new CarApiException(CarApiFailure.BadName, $"'{file}' is not a map file the car serves");
        }
        string partial = path + ".part";
        long existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(this.BaseUri, $"maps/{map}/files/{file}")))
        {
            if (existing > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existing, null);
            }
            using (HttpResponseMessage response = await this.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, null, cancellation).ConfigureAwait(false))
            {
                await CarApiClient.EnsureSuccessAsync(response).ConfigureAwait(false);
                bool isResume = response.StatusCode == HttpStatusCode.PartialContent;
                if (!isResume)
                {
                    existing = 0;
                }
                long? total = response.Content.Headers.ContentLength + existing;
                using (Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (FileStream target = new FileStream(partial, isResume ? FileMode.Append : FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[81920];
                    long written = existing;
                    int read;
                    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) > 0)
                    {
                        await target.WriteAsync(buffer, 0, read, cancellation).ConfigureAwait(false);
                        written += read;
                        if (total.HasValue && total.Value > 0)
                        {
                            progress?.Report((double)written / total.Value);
                        }
                    }
                }
                string expected = CarApiClient.Header(response, "X-Content-SHA256");
                if (expected != null)
                {
                    string actual = CarApiClient.Sha256(partial);
                    if (!string.Equals(expected.Trim(), actual, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(partial);
                        throw new CarApiException(CarApiFailure.Checksum, $"{file} did not match the car's checksum");
                    }
                }
            }
        }
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(partial, path);
    }

    /// <summary>
    /// True for a map name the car accepts: letters, digits, '_' and '-'.
    /// </summary>
    public static bool IsValidName(string name)
    {
        return !string.IsNullOrEmpty(name) && CarApiClient.namePattern.IsMatch(name);
    }

    /// <summary>
    /// True for a file the car serves for a map.
    /// </summary>
    public static bool IsAllowedFile(string map, string file)
    {
        return file == map + ".pgm" || file == map + ".yaml" || file == "meta.yaml" || file == "last_pose.yaml"
            || file == "session.tar" || file == "rgbd.tar";
    }

    /// <summary>
    /// The JSON body for a map command.
    /// </summary>
    public static CarLinkValue ActionBody(CarAction action)
    {
        Dictionary<string, CarLinkValue> body = new Dictionary<string, CarLinkValue>
        {
            { "action", CarLinkValue.FromString(action.Action) },
            { "client", CarLinkValue.FromString(CarApiClient.ClientName) },
            { "confirm", CarLinkValue.FromBool(true) },
        };
        if (action.Name != null)
        {
            body["name"] = CarLinkValue.FromString(action.Name);
        }
        if (action.From != null)
        {
            body["from"] = CarLinkValue.FromString(action.From);
        }
        if (action.HasPose)
        {
            body["pose"] = CarLinkValue.FromDoubles(new[] { action.PoseX, action.PoseY, action.PoseYaw });
        }
        if (action.Global)
        {
            body["global"] = CarLinkValue.FromBool(true);
        }
        if (action.Localizer != null)
        {
            body["localizer"] = CarLinkValue.FromString(action.Localizer);
        }
        if (action.Record != null)
        {
            body["record"] = CarLinkValue.FromString(action.Record);
        }
        return CarLinkValue.FromMap(body);
    }

    public void Dispose()
    {
        this.http.Dispose();
    }
    #endregion

    private static readonly Regex namePattern = new Regex("^[A-Za-z0-9_-]+$");

    private readonly HttpClient http;

    private async Task<CarLinkValue> GetJsonAsync(string relative, CancellationToken cancellation)
    {
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(this.BaseUri, relative)))
        {
            return await this.SendForJsonAsync(request, cancellation).ConfigureAwait(false);
        }
    }

    private async Task<CarLinkValue> PostJsonAsync(string relative, CarLinkValue body, CancellationToken cancellation)
    {
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(this.BaseUri, relative)))
        {
            request.Content = new StringContent(CarLinkJson.Write(body), Encoding.UTF8, "application/json");
            return await this.SendForJsonAsync(request, cancellation).ConfigureAwait(false);
        }
    }

    private async Task<CarLinkValue> SendForJsonAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        using (HttpResponseMessage response = await this.SendAsync(request, HttpCompletionOption.ResponseContentRead, CarApiClient.RequestTimeout, cancellation).ConfigureAwait(false))
        {
            await CarApiClient.EnsureSuccessAsync(response).ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            try
            {
                return CarLinkJson.Parse(text);
            }
            catch (CarLinkFormatException exception)
            {
                throw new CarApiException(CarApiFailure.Format, exception.Message);
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completion, double? timeoutSeconds, CancellationToken cancellation)
    {
        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            if (timeoutSeconds.HasValue)
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.Value));
            }
            try
            {
                return await this.http.SendAsync(request, completion, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new CarApiException(CarApiFailure.Timeout, $"no answer from {this.BaseUri.Host}:{this.BaseUri.Port}");
            }
            catch (HttpRequestException exception)
            {
                throw CarApiClient.Classify(exception, this.BaseUri);
            }
        }
    }

    private static CarApiException Classify(Exception exception, Uri uri)
    {
        string target = $"{uri.Host}:{uri.Port}";
        for (Exception inner = exception; inner != null; inner = inner.InnerException)
        {
            if (inner is SocketException socket)
            {
                switch (socket.SocketErrorCode)
                {
                    case SocketError.ConnectionRefused:
                        return new CarApiException(CarApiFailure.Refused, $"{target} refused the connection");
                    case SocketError.TimedOut:
                        return new CarApiException(CarApiFailure.Timeout, $"no answer from {target}");
                    default:
                        return new CarApiException(CarApiFailure.NoRoute, $"cannot reach {target}: {socket.SocketErrorCode}");
                }
            }
            if (inner is WebException web && web.Status == WebExceptionStatus.NameResolutionFailure)
            {
                return new CarApiException(CarApiFailure.NoRoute, $"cannot find {uri.Host}");
            }
        }
        return new CarApiException(CarApiFailure.NoRoute, $"cannot reach {target}: {exception.Message}");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        int status = (int)response.StatusCode;
        string message = null;
        try
        {
            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            CarLinkValue body = CarLinkJson.Parse(text);
            message = body["message"].Kind == CarLinkValueKind.String ? body["message"].AsString() : null;
        }
        catch (CarLinkFormatException)
        {
            // Not a JSON error body; the status alone describes it
        }
        message = message ?? response.ReasonPhrase ?? "error";
        throw new CarApiException(status == 409 ? CarApiFailure.Busy : CarApiFailure.Http, $"car returned {status}: {message}", status);
    }

    private static T Parse<T>(Func<T> parse)
    {
        try
        {
            return parse();
        }
        catch (CarLinkFormatException exception)
        {
            throw new CarApiException(CarApiFailure.Format, exception.Message);
        }
    }

    private static void CheckName(string name)
    {
        if (!CarApiClient.IsValidName(name))
        {
            throw new CarApiException(CarApiFailure.BadName, $"'{name}' is not a valid map name");
        }
    }

    private static string Header(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out IEnumerable<string> values) || response.Content.Headers.TryGetValues(name, out values))
        {
            foreach (string value in values)
            {
                return value;
            }
        }
        return null;
    }

    private static string Sha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
